using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Enums;
using Core.Models;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services.Email;
using Services.Integrations.SendGrid;

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

        // Default loop: every 60s. Configurable via AutoReviews:ReviewRequestAfterComplete:LoopSeconds
        private static readonly TimeSpan DefaultLoopDelay = TimeSpan.FromSeconds(60);

        public ReviewRequestHostedService(IServiceScopeFactory scopeFactory, ILogger<ReviewRequestHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Reviews] Unexpected error in review request job.");
                }

                var delay = DefaultLoopDelay;
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                    var sec = cfg.GetValue<int?>("AutoReviews:ReviewRequestAfterComplete:LoopSeconds");
                    if (sec.HasValue && sec.Value >= 10) delay = TimeSpan.FromSeconds(sec.Value);
                }
                catch { /* ignore */ }

                await Task.Delay(delay, stoppingToken);
            }
        }

        private async Task RunOnceAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();
            var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var emailSvc = scope.ServiceProvider.GetRequiredService<IReviewRequestEmailService>();
            var sgOpt = scope.ServiceProvider.GetRequiredService<IOptions<SendGridOptions>>().Value;

            var enabled = cfg.GetValue("AutoReviews:ReviewRequestAfterComplete:Enabled", true);
            if (!enabled)
            {
                _logger.LogDebug("[Reviews] AutoReviews:ReviewRequestAfterComplete disabled.");
                return;
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

            // 1) Ensure dispatches exist for eligible completions
            var completions = await db.AppointmentCompletions.AsNoTracking()
                .Where(c => c.CompletedAt <= cutoff && c.CompletedAt >= lookback)
                .OrderBy(c => c.CompletedAt)
                .Take(batchSize)
                .ToListAsync(ct);

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

            foreach (var d in pendings)
            {
                try
                {
                    d.AttemptCount += 1;
                    d.LastAttemptAtUtc = nowUtc;
                    d.LastError = null;
                    d.UpdatedDate = nowUtc;
                    db.AppointmentReviewRequestDispatches.Update(d);
                    await db.SaveChangesAsync(ct);

                    var review = await db.Reviews.AsNoTracking().FirstOrDefaultAsync(r => r.Id == d.ReviewId, ct);
                    if (review == null || review.PublicToken == null)
                        throw new InvalidOperationException("Review not found or missing token.");

                    var appointment = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == review.AppointmentId, ct);
                    if (appointment == null)
                        throw new InvalidOperationException("Appointment not found.");

                    var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == d.CustomerId, ct);
                    if (customer == null || !customer.ReceiveEmail || string.IsNullOrWhiteSpace(customer.Email))
                        throw new InvalidOperationException("Customer has no email or opted out.");

                    // Build review URL
                    var baseUrl = cfg.GetValue<string>("AutoReviews:ReviewRequestAfterComplete:PublicReviewFormBaseUrl");
                    if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = sgOpt.PublicReviewFormBaseUrl;
                    if (string.IsNullOrWhiteSpace(baseUrl))
                        throw new InvalidOperationException("PublicReviewFormBaseUrl is not configured.");

                    var reviewUrl = $"{baseUrl.TrimEnd('/')}/{review.PublicToken.Value}";

                    string? addressLine = null;
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
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Reviews] Failed to send review request. dispatchId={Id} completionId={CompletionId}", d.Id, d.AppointmentCompletionId);
                    d.LastError = ex.Message;
                    d.Status = d.AttemptCount >= maxAttempts ? ReviewRequestDispatchStatus.Failed : ReviewRequestDispatchStatus.Pending;
                    d.UpdatedDate = nowUtc;
                    db.AppointmentReviewRequestDispatches.Update(d);
                    await db.SaveChangesAsync(ct);
                }
            }
        }
    }
}
