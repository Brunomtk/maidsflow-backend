using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Core.Enums;
using Core.Enums.Messaging;
using Core.Models;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services;
using Services.Email;
using Services.Integrations.SendGrid;
using Services.Localization;

namespace ControlApi.BackgroundJobs
{
    /// <summary>
    /// Auto job: when an appointment occurrence is completed, wait N minutes and then:
    /// - create (or reuse) a Review with PublicToken
    /// - send an email to the Customer with the public review link
    ///
    /// Uses AppointmentReviewRequestDispatches for idempotency and retries.
    /// </summary>
    public class ReviewRequestHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReviewRequestHostedService> _logger;
        private readonly IBackgroundJobMonitorService _jobMonitor;

        // Default loop: every 60s. Configurable via AutoReviews:ReviewRequestAfterComplete:LoopSeconds
        private static readonly TimeSpan DefaultLoopDelay = TimeSpan.FromSeconds(60);

        public ReviewRequestHostedService(IServiceScopeFactory scopeFactory, ILogger<ReviewRequestHostedService> logger, IBackgroundJobMonitorService jobMonitor)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _jobMonitor = jobMonitor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            await _jobMonitor.EnsureDefaultsRegisteredAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = DefaultLoopDelay;
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                    var sec = cfg.GetValue<int?>("AutoReviews:ReviewRequestAfterComplete:LoopSeconds");
                    if (sec.HasValue && sec.Value >= 10) delay = TimeSpan.FromSeconds(sec.Value);
                }
                catch { /* ignore */ }

                var nextRunUtc = DateTime.UtcNow.Add(delay);
                var run = await _jobMonitor.MarkStartedAsync(BackgroundJobKeys.ReviewRequest, "Review Request", "Reviews", nextRunUtc, stoppingToken);
                try
                {
                    var result = await RunOnceAsync(stoppingToken);
                    await _jobMonitor.MarkSucceededAsync(run, result.summary, result.processed, result.succeeded, result.failed, nextRunUtc, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Reviews] Unexpected error in review request job.");
                    await _jobMonitor.MarkFailedAsync(run, ex, "Unexpected error in review request job.", nextPlannedRunAtUtc: nextRunUtc, ct: stoppingToken);
                }

                await Task.Delay(delay, stoppingToken);
            }
        }

        private async Task<(int processed, int succeeded, int failed, string summary)> RunOnceAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();
            var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var emailSvc = scope.ServiceProvider.GetRequiredService<IReviewRequestEmailService>();
            var sgOpt = scope.ServiceProvider.GetRequiredService<IOptions<SendGridOptions>>().Value;
            var loc = scope.ServiceProvider.GetRequiredService<IMessageLocalizer>();
            var langResolver = scope.ServiceProvider.GetRequiredService<IRecipientLanguageResolver>();

            var enabled = cfg.GetValue("AutoReviews:ReviewRequestAfterComplete:Enabled", true);
            if (!enabled)
            {
                _logger.LogDebug("[Reviews] AutoReviews:ReviewRequestAfterComplete disabled.");
                return (0, 0, 0, "Disabled by configuration.");
            }

            var delayMinutes = cfg.GetValue("AutoReviews:ReviewRequestAfterComplete:DelayMinutes", 30);
            if (delayMinutes < 1) delayMinutes = 30;

            var batchSize = cfg.GetValue("AutoReviews:ReviewRequestAfterComplete:BatchSize", 50);
            if (batchSize < 1) batchSize = 50;

            var maxAttempts = cfg.GetValue("AutoReviews:ReviewRequestAfterComplete:MaxAttempts", 6);
            if (maxAttempts < 1) maxAttempts = 6;

            var retryMinutes = cfg.GetValue("AutoReviews:ReviewRequestAfterComplete:RetryEveryMinutes", 10);
            if (retryMinutes < 1) retryMinutes = 10;

            var lookbackDays = cfg.GetValue("AutoReviews:ReviewRequestAfterComplete:LookbackDays", 14);
            if (lookbackDays < 1) lookbackDays = 14;

            var nowUtc = DateTime.UtcNow;
            var cutoff = nowUtc.AddMinutes(-delayMinutes);
            var lookback = nowUtc.AddDays(-lookbackDays);

            var processed = 0;
            var succeeded = 0;
            var failures = 0;
            var dispatchesCreated = 0;

            // 1) Ensure dispatches exist for eligible completions
            var completions = await db.AppointmentCompletions.AsNoTracking()
                .Where(c => c.CompletedAt <= cutoff && c.CompletedAt >= lookback)
                .OrderBy(c => c.CompletedAt)
                .Take(batchSize)
                .ToListAsync(ct);

            processed += completions.Count;

            foreach (var comp in completions)
            {
                // Skip if already has a dispatch
                var exists = await db.AppointmentReviewRequestDispatches.AsNoTracking()
                    .AnyAsync(d => d.AppointmentCompletionId == comp.Id, ct);
                if (exists) continue;

                // Resolve appointment + customer
                var appointment = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == comp.AppointmentId, ct);
                if (appointment == null) continue;

                var customerId = comp.CustomerIdSnapshot ?? appointment.CustomerId;
                if (!customerId.HasValue) continue;

                var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId.Value, ct);
                if (customer == null) continue;
                if (!customer.ReceiveEmail) continue;
                if (string.IsNullOrWhiteSpace(customer.Email)) continue;

                // Create/reuse review for this appointment
                var review = await db.Reviews.FirstOrDefaultAsync(r => r.AppointmentId == appointment.Id, ct);
                if (review == null)
                {
                    var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(co => co.Id == appointment.CompanyId, ct);

                    int? professionalId = null;
                    string? professionalName = null;
                    var proIds = comp.ProfessionalIdsSnapshot;
                    if ((proIds == null || proIds.Count == 0) && appointment.ProfessionalIds != null)
                        proIds = appointment.ProfessionalIds;

                    if (proIds != null && proIds.Count > 0)
                    {
                        professionalId = proIds[0];
                        var pro = await db.Professionals.AsNoTracking().FirstOrDefaultAsync(p => p.Id == professionalId.Value, ct);
                        professionalName = pro?.Name;
                    }

                    review = new Review
                    {
                        CustomerId = customerId.Value,
                        CustomerAddressId = comp.CustomerAddressIdSnapshot ?? appointment.CustomerAddressId,
                        CustomerName = customer.Name,
                        ProfessionalId = professionalId,
                        ProfessionalName = professionalName,
                        TeamId = comp.TeamIdSnapshot ?? appointment.TeamId,
                        TeamName = null,
                        CompanyId = appointment.CompanyId,
                        CompanyName = company?.Name,
                        AppointmentId = appointment.Id,
                        Date = comp.OccurrenceStart,
                        ServiceType = appointment.Type.ToString(),
                        Status = ReviewStatus.Pending,
                        Rating = 0,
                        Comment = null,
                        PublicToken = Guid.NewGuid(),
                        SubmittedAt = null,
                        CreatedDate = nowUtc,
                        UpdatedDate = nowUtc
                    };

                    db.Reviews.Add(review);
                    await db.SaveChangesAsync(ct);
                }
                else
                {
                    // Ensure token exists
                    if (review.PublicToken == null)
                    {
                        review.PublicToken = Guid.NewGuid();
                        review.UpdatedDate = nowUtc;
                        db.Reviews.Update(review);
                        await db.SaveChangesAsync(ct);
                    }
                }

                // Create dispatch (Pending)
                try
                {
                    var dispatch = new AppointmentReviewRequestDispatch
                    {
                        CompanyId = appointment.CompanyId,
                        AppointmentCompletionId = comp.Id,
                        ReviewId = review.Id,
                        CustomerId = customerId.Value,
                        RecipientEmail = customer.Email!,
                        Status = ReviewRequestDispatchStatus.Pending,
                        AttemptCount = 0,
                        LastAttemptAtUtc = null,
                        SentAtUtc = null,
                        LastError = null,
                        CreatedDate = nowUtc,
                        UpdatedDate = nowUtc
                    };
                    db.AppointmentReviewRequestDispatches.Add(dispatch);
                    await db.SaveChangesAsync(ct);
                    dispatchesCreated += 1;
                }
                catch (DbUpdateException)
                {
                    // Another instance created it
                    continue;
                }
            }

            // 2) Process pending dispatches (retry-safe)
            var retryCutoff = nowUtc.AddMinutes(-retryMinutes);

            var pendings = await db.AppointmentReviewRequestDispatches
                .Where(d => d.Status != ReviewRequestDispatchStatus.Sent
                            && d.AttemptCount < maxAttempts
                            && (d.LastAttemptAtUtc == null || d.LastAttemptAtUtc <= retryCutoff))
                .OrderBy(d => d.CreatedDate)
                .Take(batchSize)
                .ToListAsync(ct);

            processed += pendings.Count;

            foreach (var d in pendings)
            {
                Appointment? appointment = null;
                AppointmentCompletion? completion = null;
                Review? review = null;
                Customer? customer = null;
                string? reviewUrl = null;
                string? addressLine = null;
                string? subject = null;
                string? plainText = null;

                try
                {
                    d.AttemptCount += 1;
                    d.LastAttemptAtUtc = nowUtc;
                    d.LastError = null;
                    d.UpdatedDate = nowUtc;
                    db.AppointmentReviewRequestDispatches.Update(d);
                    await db.SaveChangesAsync(ct);

                    review = await db.Reviews.AsNoTracking().FirstOrDefaultAsync(r => r.Id == d.ReviewId, ct);
                    if (review == null || review.PublicToken == null)
                        throw new InvalidOperationException("Review not found or missing token.");

                    appointment = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == review.AppointmentId, ct);
                    if (appointment == null)
                        throw new InvalidOperationException("Appointment not found.");

                    completion = await db.AppointmentCompletions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == d.AppointmentCompletionId, ct);

                    customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == d.CustomerId, ct);
                    if (customer == null || !customer.ReceiveEmail || string.IsNullOrWhiteSpace(customer.Email))
                        throw new InvalidOperationException("Customer has no email or opted out.");

                    var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(co => co.Id == appointment.CompanyId, ct);
                    var companyName = company?.Name ?? string.Empty;

                    // Build review URL
                    var baseUrl = cfg.GetValue<string>("AutoReviews:ReviewRequestAfterComplete:PublicReviewFormBaseUrl");
                    if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = sgOpt.PublicReviewFormBaseUrl;
                    if (string.IsNullOrWhiteSpace(baseUrl))
                        throw new InvalidOperationException("PublicReviewFormBaseUrl is not configured.");

                    reviewUrl = ReviewPublicLinkBuilder.Build(baseUrl, review.PublicToken.Value);

                    if (review.CustomerAddressId.HasValue)
                    {
                        var addr = await db.CustomerAddresses.AsNoTracking().FirstOrDefaultAsync(a => a.Id == review.CustomerAddressId.Value, ct);
                        if (addr != null)
                        {
                            addressLine = string.IsNullOrWhiteSpace(addr.AddressLine1)
                                ? addr.Label
                                : $"{addr.Label} • {addr.AddressLine1}";
                        }
                    }

                    subject = string.IsNullOrWhiteSpace(sgOpt.ReviewRequestSubject)
                        ? "How was your service?"
                        : sgOpt.ReviewRequestSubject.Trim();

                    var customerLanguage = await langResolver.ForCustomerAsync(customer.Id, ct);
                    var (_, renderedPlainText) = ReviewRequestEmailTemplate.Render(new ReviewRequestEmailTemplate.Model(
                        CustomerName: customer.Name ?? string.Empty,
                        CompanyName: companyName,
                        AppointmentTitle: string.IsNullOrWhiteSpace(appointment.Title) ? "Your service" : appointment.Title,
                        AppointmentStartLocal: review.Date,
                        AddressLine: addressLine,
                        ReviewUrl: reviewUrl,
                        SupportUrl: string.IsNullOrWhiteSpace(sgOpt.SupportUrl) ? string.Empty : sgOpt.SupportUrl.Trim()
                    ), loc, customerLanguage);
                    plainText = renderedPlainText;

                    await emailSvc.SendReviewRequestAsync(
                        companyId: d.CompanyId,
                        customerId: d.CustomerId,
                        reviewUrl: reviewUrl,
                        appointmentTitle: appointment.Title,
                        appointmentStartLocal: review.Date,
                        addressLine: addressLine,
                        ct: ct);

                    // Mark sent
                    d.Status = ReviewRequestDispatchStatus.Sent;
                    d.SentAtUtc = nowUtc;
                    d.UpdatedDate = nowUtc;
                    db.AppointmentReviewRequestDispatches.Update(d);
                    await db.SaveChangesAsync(ct);

                    await AddReviewEmailLogAsync(db, appointment, completion, d, customer.Email!.Trim(), subject, plainText, reviewUrl, review.Id, customer.Name, AppointmentMessageStatus.Sent, "Sent", nowUtc, null, null, ct);
                    succeeded += 1;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Reviews] Failed to send review request. dispatchId={Id} completionId={CompletionId}", d.Id, d.AppointmentCompletionId);
                    d.LastError = ex.Message;
                    d.Status = d.AttemptCount >= maxAttempts ? ReviewRequestDispatchStatus.Failed : ReviewRequestDispatchStatus.Pending;
                    d.UpdatedDate = nowUtc;
                    db.AppointmentReviewRequestDispatches.Update(d);
                    await db.SaveChangesAsync(ct);

                    if (appointment != null)
                    {
                        await AddReviewEmailLogAsync(db, appointment, completion, d, d.RecipientEmail, subject ?? (string.IsNullOrWhiteSpace(sgOpt.ReviewRequestSubject) ? "How was your service?" : sgOpt.ReviewRequestSubject.Trim()), plainText, reviewUrl, review?.Id, customer?.Name, AppointmentMessageStatus.Failed, "Failed", null, ex.Message, ex.ToString(), ct);
                    }

                    failures += 1;
                }
            }

            return (processed, succeeded, failures, $"CompletionsScanned={completions.Count}, DispatchesCreated={dispatchesCreated}, PendingProcessed={pendings.Count}, EmailsSent={succeeded}, Failures={failures}");
        }

        private static async Task AddReviewEmailLogAsync(
            DbContextClass db,
            Appointment appointment,
            AppointmentCompletion? completion,
            AppointmentReviewRequestDispatch dispatch,
            string? recipientEmail,
            string? subject,
            string? bodyText,
            string? reviewUrl,
            int? reviewId,
            string? customerName,
            AppointmentMessageStatus status,
            string providerStatus,
            DateTime? sentAtUtc,
            string? lastError,
            string? lastErrorRaw,
            CancellationToken ct)
        {
            var occurrenceStartUtc = completion?.OccurrenceStart.Kind == DateTimeKind.Utc ? completion.OccurrenceStart : (DateTime?)null;
            var occurrenceEndUtc = completion?.OccurrenceEnd.Kind == DateTimeKind.Utc ? completion.OccurrenceEnd : (DateTime?)null;

            var lastAttempt = await db.AppointmentMessageLogs.AsNoTracking()
                .Where(x => x.AppointmentId == appointment.Id
                    && x.Kind == AppointmentMessageKind.ReviewRequestEmail
                    && x.Channel == AppointmentMessageChannel.Email
                    && ((occurrenceStartUtc == null && occurrenceEndUtc == null)
                        || (x.OccurrenceStartUtc == occurrenceStartUtc && x.OccurrenceEndUtc == occurrenceEndUtc)))
                .OrderByDescending(x => x.Attempt)
                .Select(x => x.Attempt)
                .FirstOrDefaultAsync(ct);
            var nextAttempt = lastAttempt <= 0 ? 1 : lastAttempt + 1;

            var payloadJson = JsonSerializer.Serialize(new
            {
                dispatchId = dispatch.Id,
                appointmentCompletionId = dispatch.AppointmentCompletionId,
                reviewId,
                reviewUrl,
                appointmentId = appointment.Id,
                appointmentTitle = appointment.Title,
                appointmentStart = appointment.Start,
                appointmentEnd = appointment.End,
                occurrenceStartUtc,
                occurrenceEndUtc,
                seriesId = appointment.SeriesId,
                companyId = appointment.CompanyId,
                customerId = dispatch.CustomerId,
                customerName,
                customerAddressId = appointment.CustomerAddressId,
                automatic = true
            });

            await db.AppointmentMessageLogs.AddAsync(new AppointmentMessageLog
            {
                AppointmentId = appointment.Id,
                SeriesId = appointment.SeriesId,
                OccurrenceStartUtc = occurrenceStartUtc,
                OccurrenceEndUtc = occurrenceEndUtc,
                Kind = AppointmentMessageKind.ReviewRequestEmail,
                Channel = AppointmentMessageChannel.Email,
                Status = status,
                ScheduledForUtc = dispatch.LastAttemptAtUtc,
                SentAtUtc = sentAtUtc,
                Attempt = nextAttempt,
                RequestedByUserId = null,
                RequestedByRole = "system",
                RecipientEmail = recipientEmail,
                Subject = subject,
                BodyText = bodyText,
                TemplateKey = "review-request-email",
                PayloadJson = payloadJson,
                Provider = "SendGrid",
                ProviderStatus = providerStatus,
                LastError = lastError,
                LastErrorRaw = lastErrorRaw,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            }, ct);
            await db.SaveChangesAsync(ct);
        }
    }
}
