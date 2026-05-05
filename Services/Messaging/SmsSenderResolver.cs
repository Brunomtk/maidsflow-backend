using Core.DTO.Messaging;
using Core.Models;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Services.Messaging
{
    /// <summary>
    /// Decides the right SMS sender for a company applying these rules in order:
    ///
    ///  1. If profile.Status == Approved AND TwilioFromPhoneE164 is set
    ///     → CanSend, use the company-owned Twilio number.
    ///
    ///  2. If profile.Status == Suspended OR Rejected → blocked.
    ///
    ///  3. If profile is missing OR Status == Trial / PendingReview / NeedsChanges /
    ///     ReadyForTwilio / SubmittedToTwilio AND we are still inside the trial window
    ///     → CanSend on the MaidsFlow shared sandbox number.
    ///
    ///  4. Otherwise (trial expired, no approved company number) → blocked.
    /// </summary>
    public class SmsSenderResolver : ISmsSenderResolver
    {
        private readonly DbContextClass _db;
        private readonly ILogger<SmsSenderResolver> _logger;
        private readonly string _trialFromPhone;
        private const int TRIAL_DAYS = 15;

        public SmsSenderResolver(DbContextClass db, IConfiguration config, ILogger<SmsSenderResolver> logger)
        {
            _db = db;
            _logger = logger;
            // Pull from Twilio:From or appsettings; fallback to historical sandbox number used by n8n
            _trialFromPhone =
                config["Twilio:From"] ??
                config["Twilio:TrialFromPhone"] ??
                "+18443146425";
        }

        public async Task<SmsSenderDecisionDTO> ResolveAsync(int companyId, CancellationToken ct = default)
        {
            var profile = await _db.CompanyMessagingProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.CompanyId == companyId, ct);

            // Auto-create trial profile on the fly if it doesn't exist.
            // This way every existing company gets a 15-day window starting on first send attempt.
            if (profile == null)
            {
                profile = new CompanyMessagingProfile
                {
                    CompanyId = companyId,
                    Status = "Trial",
                    SmsEnabled = true,
                    TrialStartedAtUtc = DateTime.UtcNow,
                    TrialEndsAtUtc = DateTime.UtcNow.AddDays(TRIAL_DAYS),
                    DefaultTrialFromPhoneE164 = _trialFromPhone,
                };
                _db.CompanyMessagingProfiles.Add(profile);
                try { await _db.SaveChangesAsync(ct); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to auto-create trial profile for company {Id}", companyId); }
            }

            var status = profile.Status ?? "Trial";
            var trialEnds = profile.TrialEndsAtUtc;
            var nowUtc = DateTime.UtcNow;

            // 1) Suspended/Rejected — always blocked
            if (status == "Suspended")
            {
                return Block(companyId, status, trialEnds, "MessagingSuspended");
            }
            if (status == "Rejected")
            {
                return Block(companyId, status, trialEnds, "MessagingRejected");
            }

            // 2) Approved with company number → use it
            if (status == "Approved" && !string.IsNullOrWhiteSpace(profile.TwilioFromPhoneE164))
            {
                return new SmsSenderDecisionDTO
                {
                    CanSend = true,
                    Reason = "Approved",
                    FromPhoneE164 = profile.TwilioFromPhoneE164,
                    SenderSource = "CompanyTwilioNumber",
                    CompanyId = companyId,
                    Status = status,
                    TrialEndsAtUtc = trialEnds,
                    TwilioMessagingServiceSid = profile.TwilioMessagingServiceSid,
                };
            }

            // 3) Approved but missing number config → block (admin must finish setup)
            if (status == "Approved")
            {
                return Block(companyId, status, trialEnds, "ApprovedButNoCompanyNumber");
            }

            // 4) ExpiredTrial → block immediately
            if (status == "ExpiredTrial")
            {
                return Block(companyId, status, trialEnds, "TrialExpiredNoApprovedCompanyNumber");
            }

            // 5) Trial / PendingReview / NeedsChanges / ReadyForTwilio / SubmittedToTwilio
            //    → use trial sandbox number IF still inside the trial window.
            if (trialEnds.HasValue && nowUtc <= trialEnds.Value)
            {
                var trialPhone = !string.IsNullOrWhiteSpace(profile.DefaultTrialFromPhoneE164)
                    ? profile.DefaultTrialFromPhoneE164
                    : _trialFromPhone;

                return new SmsSenderDecisionDTO
                {
                    CanSend = true,
                    Reason = "TrialActive",
                    FromPhoneE164 = trialPhone,
                    SenderSource = "MaidsFlowTrialNumber",
                    CompanyId = companyId,
                    Status = status,
                    TrialEndsAtUtc = trialEnds,
                };
            }

            // 6) No trial window or expired → flip status to ExpiredTrial and block
            profile.Status = "ExpiredTrial";
            try
            {
                _db.CompanyMessagingProfiles.Update(profile);
                await _db.SaveChangesAsync(ct);

                _db.CompanyMessagingAuditLogs.Add(new CompanyMessagingAuditLog
                {
                    CompanyId = companyId,
                    Action = "TrialExpired",
                    Notes = "Auto-set by SmsSenderResolver because trial ended without Twilio approval.",
                    CreatedAtUtc = nowUtc,
                });
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to mark trial expired for company {Id}", companyId);
            }
            return Block(companyId, "ExpiredTrial", trialEnds, "TrialExpiredNoApprovedCompanyNumber");
        }

        private static SmsSenderDecisionDTO Block(int companyId, string status, DateTime? trialEnds, string reason) =>
            new()
            {
                CanSend = false,
                Reason = reason,
                FromPhoneE164 = null,
                SenderSource = null,
                CompanyId = companyId,
                Status = status,
                TrialEndsAtUtc = trialEnds,
            };
    }
}
