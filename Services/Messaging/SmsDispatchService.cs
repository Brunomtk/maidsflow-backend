using Core.DTO.Messaging;
using Core.Enums.Messaging;
using Core.Models;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Integrations.Twilio;

namespace Services.Messaging
{
    /// <summary>
    /// Centralized SMS dispatch — single entry point for ALL outbound SMS in the system.
    ///
    /// Responsibilities:
    ///   1. Consult <see cref="ISmsSenderResolver"/> to pick FROM number / decide if blocked
    ///   2. Call Twilio with the resolved FROM
    ///   3. Persist <see cref="AppointmentMessageLog"/> with full audit (sender, source, status)
    ///   4. On failure: schedule a retry with exponential backoff (handled by SmsRetryHostedService)
    ///   5. On block: log as Blocked WITHOUT calling Twilio
    ///
    /// This replaces the previous "fire and forget" pattern + the n8n workflow's call sequence.
    /// </summary>
    public interface ISmsDispatchService
    {
        /// <summary>Try to send right now. Returns whether it was sent, blocked, or queued for retry.</summary>
        Task<SmsDispatchResult> DispatchAsync(SmsDispatchRequest request, CancellationToken ct = default);

        /// <summary>
        /// Used by SmsRetryHostedService — retry a single existing log entry (it must be in Failed state).
        /// </summary>
        Task<SmsDispatchResult> RetryAsync(int messageLogId, CancellationToken ct = default);
    }

    public class SmsDispatchRequest
    {
        public int CompanyId { get; set; }
        public int AppointmentId { get; set; }
        public Guid? SeriesId { get; set; }
        public DateTime? OccurrenceStartUtc { get; set; }
        public DateTime? OccurrenceEndUtc { get; set; }

        public AppointmentMessageKind Kind { get; set; } = AppointmentMessageKind.ConfirmationSms24h;
        public required string ToPhoneE164 { get; set; }
        public required string Body { get; set; }
        public string? TemplateKey { get; set; }
        public int? RequestedByUserId { get; set; }
        public string? RequestedByRole { get; set; }
        public string? Subject { get; set; }
        public string? PayloadJson { get; set; }
    }

    public enum SmsDispatchOutcome { Sent, Blocked, FailedWillRetry, FailedTerminal }

    public class SmsDispatchResult
    {
        public SmsDispatchOutcome Outcome { get; set; }
        public int? MessageLogId { get; set; }
        public string? ProviderMessageId { get; set; }
        public string? FromPhoneE164 { get; set; }
        public string? SenderSource { get; set; }
        public string? BlockReason { get; set; }
        public string? Error { get; set; }
        public int Attempt { get; set; }
    }

    public class SmsDispatchService : ISmsDispatchService
    {
        private readonly DbContextClass _db;
        private readonly ISmsSenderResolver _resolver;
        private readonly ITwilioSmsSender _twilio;
        private readonly ILogger<SmsDispatchService> _logger;

        // Retry policy: exponential backoff
        private const int MAX_ATTEMPTS = 5;
        private static readonly TimeSpan[] BACKOFF = new[]
        {
            TimeSpan.FromMinutes(1),    // attempt 1 -> 2: wait 1 min
            TimeSpan.FromMinutes(5),    // attempt 2 -> 3: wait 5 min
            TimeSpan.FromMinutes(15),   // attempt 3 -> 4: wait 15 min
            TimeSpan.FromMinutes(60),   // attempt 4 -> 5: wait 1 hour
        };

        public SmsDispatchService(
            DbContextClass db,
            ISmsSenderResolver resolver,
            ITwilioSmsSender twilio,
            ILogger<SmsDispatchService> logger)
        {
            _db = db;
            _resolver = resolver;
            _twilio = twilio;
            _logger = logger;
        }

        public async Task<SmsDispatchResult> DispatchAsync(SmsDispatchRequest request, CancellationToken ct = default)
        {
            // 1) Resolve sender / compliance
            var decision = await _resolver.ResolveAsync(request.CompanyId, ct);

            // 2) Create log entry up-front (so we always have audit trail even on crash mid-send)
            var log = new AppointmentMessageLog
            {
                AppointmentId = request.AppointmentId,
                SeriesId = request.SeriesId,
                OccurrenceStartUtc = request.OccurrenceStartUtc,
                OccurrenceEndUtc = request.OccurrenceEndUtc,
                Kind = request.Kind,
                Channel = AppointmentMessageChannel.Sms,
                Status = AppointmentMessageStatus.Pending,
                ScheduledForUtc = DateTime.UtcNow,
                Attempt = 1,
                RequestedByUserId = request.RequestedByUserId,
                RequestedByRole = request.RequestedByRole,
                RecipientPhoneE164 = request.ToPhoneE164,
                Subject = request.Subject,
                BodyText = request.Body,
                TemplateKey = request.TemplateKey,
                PayloadJson = request.PayloadJson,
                Provider = "Twilio",
                MessagingProfileStatus = decision.Status,
                SenderPhoneE164 = decision.FromPhoneE164,
                SenderSource = decision.SenderSource,
            };
            _db.AppointmentMessageLogs.Add(log);
            await _db.SaveChangesAsync(ct);

            // 3) If blocked by compliance → mark and bail
            if (!decision.CanSend)
            {
                log.Status = AppointmentMessageStatus.Failed;
                log.WasBlockedByMessagingPolicy = true;
                log.MessagingBlockReason = decision.Reason;
                log.LastError = $"Blocked by messaging policy: {decision.Reason}";
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("SMS blocked for company {CompanyId} appt {AppointmentId}: {Reason}",
                    request.CompanyId, request.AppointmentId, decision.Reason);

                return new SmsDispatchResult
                {
                    Outcome = SmsDispatchOutcome.Blocked,
                    MessageLogId = log.Id,
                    BlockReason = decision.Reason,
                    FromPhoneE164 = null,
                    SenderSource = null,
                    Attempt = 1,
                };
            }

            // 4) Send via Twilio
            return await TrySendAndUpdateLogAsync(log, decision.FromPhoneE164!, ct);
        }

        public async Task<SmsDispatchResult> RetryAsync(int messageLogId, CancellationToken ct = default)
        {
            var log = await _db.AppointmentMessageLogs.FirstOrDefaultAsync(l => l.Id == messageLogId, ct);
            if (log == null)
                return new SmsDispatchResult { Outcome = SmsDispatchOutcome.FailedTerminal, Error = "Log not found." };

            if (log.Status != AppointmentMessageStatus.Failed)
                return new SmsDispatchResult
                {
                    Outcome = SmsDispatchOutcome.FailedTerminal,
                    MessageLogId = log.Id,
                    Error = "Log is not in Failed state, cannot retry."
                };

            if (log.WasBlockedByMessagingPolicy)
                return new SmsDispatchResult
                {
                    Outcome = SmsDispatchOutcome.Blocked,
                    MessageLogId = log.Id,
                    BlockReason = log.MessagingBlockReason,
                    Error = "Cannot retry — was blocked by compliance policy. Resolve compliance first."
                };

            if (log.Attempt >= MAX_ATTEMPTS)
            {
                _logger.LogWarning("SMS retry abandoned (max attempts) for log {LogId}", log.Id);
                return new SmsDispatchResult
                {
                    Outcome = SmsDispatchOutcome.FailedTerminal,
                    MessageLogId = log.Id,
                    Error = "Max retry attempts reached."
                };
            }

            // Re-resolve sender (could have changed: trial expired, company got approved, etc.)
            // Use the appointment's company. We need to load it.
            var appt = await _db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == log.AppointmentId, ct);
            if (appt == null)
                return new SmsDispatchResult { Outcome = SmsDispatchOutcome.FailedTerminal, MessageLogId = log.Id, Error = "Appointment not found." };

            var decision = await _resolver.ResolveAsync(appt.CompanyId, ct);

            log.Attempt += 1;
            log.Status = AppointmentMessageStatus.Pending;
            log.MessagingProfileStatus = decision.Status;
            log.SenderPhoneE164 = decision.FromPhoneE164;
            log.SenderSource = decision.SenderSource;
            log.LastError = null;
            log.LastErrorRaw = null;
            await _db.SaveChangesAsync(ct);

            if (!decision.CanSend)
            {
                log.Status = AppointmentMessageStatus.Failed;
                log.WasBlockedByMessagingPolicy = true;
                log.MessagingBlockReason = decision.Reason;
                log.LastError = $"Blocked on retry: {decision.Reason}";
                await _db.SaveChangesAsync(ct);
                return new SmsDispatchResult
                {
                    Outcome = SmsDispatchOutcome.Blocked,
                    MessageLogId = log.Id,
                    BlockReason = decision.Reason,
                    Attempt = log.Attempt,
                };
            }

            return await TrySendAndUpdateLogAsync(log, decision.FromPhoneE164!, ct);
        }

        // ----- Internal -----

        private async Task<SmsDispatchResult> TrySendAndUpdateLogAsync(AppointmentMessageLog log, string fromE164, CancellationToken ct)
        {
            try
            {
                var (sid, raw) = await _twilio.SendSmsFromAsync(fromE164, log.RecipientPhoneE164 ?? "", log.BodyText ?? "", ct);

                log.Status = AppointmentMessageStatus.Sent;
                log.SentAtUtc = DateTime.UtcNow;
                log.ProviderMessageId = sid;
                log.ProviderStatus = "queued";
                log.LastError = null;
                log.LastErrorRaw = null;
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("SMS sent to {To} from {From} (sid={Sid}) attempt={Attempt}",
                    log.RecipientPhoneE164, fromE164, sid, log.Attempt);

                return new SmsDispatchResult
                {
                    Outcome = SmsDispatchOutcome.Sent,
                    MessageLogId = log.Id,
                    ProviderMessageId = sid,
                    FromPhoneE164 = fromE164,
                    SenderSource = log.SenderSource,
                    Attempt = log.Attempt,
                };
            }
            catch (Exception ex)
            {
                log.Status = AppointmentMessageStatus.Failed;

                // ----- Friendly LastError + raw provider response -----
                string friendlyMessage = ex.Message;
                string? rawBody = null;
                int? twilioCode = null;

                if (ex is TwilioRequestException trex)
                {
                    rawBody = trex.ResponseBody;
                    // Try to parse Twilio's JSON error body: { "code": 21211, "message": "Invalid 'To' Phone Number", ... }
                    if (!string.IsNullOrWhiteSpace(rawBody))
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(rawBody);
                            if (doc.RootElement.TryGetProperty("code", out var codeEl) && codeEl.TryGetInt32(out var c))
                                twilioCode = c;
                            if (doc.RootElement.TryGetProperty("message", out var msgEl))
                            {
                                var twMsg = msgEl.GetString();
                                if (!string.IsNullOrWhiteSpace(twMsg))
                                    friendlyMessage = twilioCode.HasValue ? $"Twilio {twilioCode}: {twMsg}" : twMsg!;
                            }
                        }
                        catch { /* keep ex.Message as fallback */ }
                    }
                }

                log.LastError = friendlyMessage;
                log.LastErrorRaw = rawBody ?? ex.ToString();

                // ----- Decide if this is a TERMINAL Twilio error (don't retry) -----
                // See: https://www.twilio.com/docs/api/errors
                //   21211 - Invalid 'To' Phone Number
                //   21214 - 'To' phone number cannot be reached
                //   21408 - Permission to send SMS to that country/region not enabled
                //   21610 - Recipient unsubscribed (STOP)
                //   21612 - 'To' not currently SMS-reachable via this carrier
                //   21614 - 'To' is not a valid mobile number
                //   30003 / 30005 / 30006 - Unreachable / unknown / landline
                bool isTerminalTwilioError = twilioCode.HasValue && (
                    twilioCode == 21211 || twilioCode == 21214 || twilioCode == 21408 ||
                    twilioCode == 21610 || twilioCode == 21612 || twilioCode == 21614 ||
                    twilioCode == 30003 || twilioCode == 30005 || twilioCode == 30006
                );
                bool isValidationError = ex is TwilioValidationException;
                bool willRetry = !isTerminalTwilioError && !isValidationError && log.Attempt < MAX_ATTEMPTS;

                // ----- Auto opt-out -----
                // Mark Customer.ReceiveSms = false so the hosted services stop queueing new logs.
                // Triggers:
                //   - Twilio 21610: customer replied STOP at carrier level. They can opt back in
                //     via START which is processed by TwilioWebhooksController.
                //   - TwilioValidationException: phone number is locally invalid (e.g. NANP area
                //     code starts with 0/1, malformed, missing). Will never work; disabling stops
                //     wasting cycles on every batch.
                bool shouldAutoDisable =
                    twilioCode == 21610 ||
                    twilioCode == 21211 || twilioCode == 21214 || twilioCode == 21614 || // Twilio: invalid To
                    ex is TwilioValidationException;                                       // local: invalid format

                if (shouldAutoDisable)
                {
                    string reason = ex is TwilioValidationException
                        ? "invalid phone format"
                        : (twilioCode == 21610 ? "21610 STOP"
                           : $"Twilio {twilioCode} invalid 'To'");
                    try
                    {
                        var appt = await _db.Appointments.AsNoTracking()
                            .Where(a => a.Id == log.AppointmentId)
                            .Select(a => new { a.CustomerId })
                            .FirstOrDefaultAsync(ct);
                        if (appt?.CustomerId != null)
                        {
                            var cust = await _db.Customers
                                .Where(c => c.Id == appt.CustomerId.Value)
                                .FirstOrDefaultAsync(ct);
                            if (cust != null && cust.ReceiveSms)
                            {
                                cust.ReceiveSms = false;
                                _logger.LogWarning("Auto-disabled ReceiveSms for customer {CustomerId} ({Phone}) — reason: {Reason}.", cust.Id, cust.Phone, reason);
                            }
                        }
                    }
                    catch (Exception autoEx)
                    {
                        _logger.LogError(autoEx, "Failed to auto-disable ReceiveSms (log {LogId}, reason {Reason}).", log.Id, reason);
                    }
                }

                int nextAttemptIdx = Math.Clamp(log.Attempt - 1, 0, BACKOFF.Length - 1);
                if (willRetry)
                {
                    log.ScheduledForUtc = DateTime.UtcNow.Add(BACKOFF[nextAttemptIdx]);
                }
                else
                {
                    log.ScheduledForUtc = null;
                }
                await _db.SaveChangesAsync(ct);

                _logger.LogWarning(ex, "SMS send failed (attempt {Attempt}) for log {LogId}; twilio_code={Code} terminal={Terminal} willRetry={Retry}",
                    log.Attempt, log.Id, twilioCode, isTerminalTwilioError || isValidationError, willRetry);

                return new SmsDispatchResult
                {
                    Outcome = willRetry ? SmsDispatchOutcome.FailedWillRetry : SmsDispatchOutcome.FailedTerminal,
                    MessageLogId = log.Id,
                    Error = friendlyMessage,
                    FromPhoneE164 = fromE164,
                    SenderSource = log.SenderSource,
                    Attempt = log.Attempt,
                };
            }
        }
    }
}
