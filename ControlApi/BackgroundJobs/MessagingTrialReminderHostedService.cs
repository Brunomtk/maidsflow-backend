using Core.Models;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services;
using Services.Integrations.SendGrid;
using Services.Localization;

namespace ControlApi.BackgroundJobs
{
    /// <summary>
    /// Sends "your SMS trial is ending" emails at D-2 and D-1 to every company
    /// whose Twilio A2P 10DLC application is still in a non-final state
    /// (Trial / PendingReview / NeedsChanges / ReadyForTwilio / SubmittedToTwilio).
    ///
    /// Idempotency is enforced via CompanyMessagingAuditLogs entries with
    /// Action = "TrialReminderD2Sent" / "TrialReminderD1Sent".
    ///
    /// Polls every 4 hours, so the average miss is ≤2 hours regardless of when
    /// the company's TrialEndsAtUtc actually crosses the 48h / 24h marks.
    /// </summary>
    public class MessagingTrialReminderHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MessagingTrialReminderHostedService> _logger;
        private readonly IBackgroundJobMonitorService _jobMonitor;

        private static readonly TimeSpan POLL = TimeSpan.FromHours(4);

        // Statuses that should still receive the reminder.
        private static readonly string[] ELIGIBLE_STATUSES =
            { "Trial", "PendingReview", "NeedsChanges", "ReadyForTwilio", "SubmittedToTwilio" };

        public MessagingTrialReminderHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<MessagingTrialReminderHostedService> logger,
            IBackgroundJobMonitorService jobMonitor)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _jobMonitor = jobMonitor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MessagingTrialReminderHostedService started; polling every {Interval}", POLL);
            await _jobMonitor.EnsureDefaultsRegisteredAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var nextRunUtc = DateTime.UtcNow.Add(POLL);
                var run = await _jobMonitor.MarkStartedAsync(
                    BackgroundJobKeys.MessagingTrialReminder,
                    "Messaging Trial Reminder (D-2 / D-1)",
                    "Messaging",
                    nextRunUtc,
                    stoppingToken);
                try
                {
                    var (sent, failed, skipped, total) = await ProcessOnceAsync(stoppingToken);
                    var summary = $"sent:{sent} failed:{failed} skipped:{skipped}";
                    await _jobMonitor.MarkSucceededAsync(run, summary, total, sent, failed, nextRunUtc, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MessagingTrialReminder loop error");
                    await _jobMonitor.MarkFailedAsync(run, ex,
                        "Unexpected error in MessagingTrialReminder.", nextPlannedRunAtUtc: nextRunUtc, ct: stoppingToken);
                }

                try { await Task.Delay(POLL, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task<(int sent, int failed, int skipped, int total)> ProcessOnceAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db        = scope.ServiceProvider.GetRequiredService<DbContextClass>();
            var sendGrid  = scope.ServiceProvider.GetRequiredService<ISendGridEmailSender>();
            var langSvc   = scope.ServiceProvider.GetRequiredService<IRecipientLanguageResolver>();
            var config    = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            var nowUtc = DateTime.UtcNow;
            // Window covers any trial ending between now+12h and now+60h —
            // catches both D-2 (~48h out) and D-1 (~24h out) with safety margin.
            var windowStart = nowUtc.AddHours(12);
            var windowEnd   = nowUtc.AddHours(60);

            var profiles = await db.CompanyMessagingProfiles.AsNoTracking()
                .Where(p => p.SmsEnabled
                            && ELIGIBLE_STATUSES.Contains(p.Status)
                            && p.TrialEndsAtUtc != null
                            && p.TrialEndsAtUtc >= windowStart
                            && p.TrialEndsAtUtc <= windowEnd)
                .ToListAsync(ct);

            if (profiles.Count == 0)
                return (0, 0, 0, 0);

            // Look up the corresponding companies in one query.
            var companyIds = profiles.Select(p => p.CompanyId).ToList();
            var companies = await db.Companies.AsNoTracking()
                .Where(c => companyIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, ct);

            // Pre-load existing audit log entries to enforce idempotency.
            var existingMarks = await db.CompanyMessagingAuditLogs.AsNoTracking()
                .Where(l => companyIds.Contains(l.CompanyId)
                            && (l.Action == "TrialReminderD2Sent" || l.Action == "TrialReminderD1Sent"))
                .Select(l => new { l.CompanyId, l.Action })
                .ToListAsync(ct);

            var portalBase = (config["App:PublicBaseUrl"]
                              ?? config["PublicAppBaseUrl"]
                              ?? "https://maidsflow.com").TrimEnd('/');
            var portalUrl = $"{portalBase}/company/sms-setup";

            int sent = 0, failed = 0, skipped = 0;

            foreach (var profile in profiles)
            {
                if (ct.IsCancellationRequested) break;

                if (!companies.TryGetValue(profile.CompanyId, out var company))
                {
                    skipped++;
                    continue;
                }
                if (!company.ReceiveEmail || string.IsNullOrWhiteSpace(company.Email))
                {
                    skipped++;
                    continue;
                }

                var hoursLeft = (profile.TrialEndsAtUtc!.Value - nowUtc).TotalHours;

                int? marker = null;       // 1 or 2
                string? action = null;
                if (hoursLeft <= 36 && hoursLeft >= 0)
                {
                    marker = 1; action = "TrialReminderD1Sent";
                }
                else if (hoursLeft > 36 && hoursLeft <= 60)
                {
                    marker = 2; action = "TrialReminderD2Sent";
                }

                if (marker == null || action == null)
                {
                    skipped++;
                    continue;
                }

                bool already = existingMarks.Any(m => m.CompanyId == profile.CompanyId && m.Action == action);
                if (already)
                {
                    skipped++;
                    continue;
                }

                var lang = await langSvc.ForCompanyAsync(profile.CompanyId, ct);

                var rendered = MessagingTrialReminderEmailTemplate.Render(
                    new MessagingTrialReminderEmailTemplate.Payload(
                        CompanyName: company.Name,
                        RecipientName: company.Name,
                        DaysLeft: marker.Value,
                        TrialEndsAtUtc: profile.TrialEndsAtUtc.Value,
                        PortalUrl: portalUrl
                    ),
                    lang);

                try
                {
                    var result = await sendGrid.SendAsync(new SendGridEmailMessage(
                        ToEmail:   company.Email!,
                        Subject:   rendered.Subject,
                        PlainText: rendered.PlainText,
                        Html:      rendered.Html,
                        ToName:    company.Name
                    ), ct);

                    if (result.Ok)
                    {
                        db.CompanyMessagingAuditLogs.Add(new CompanyMessagingAuditLog
                        {
                            CompanyId   = profile.CompanyId,
                            UserId      = null,
                            Action      = action,
                            Notes       = $"Trial reminder D-{marker} sent to {company.Email}.",
                            AfterJson   = System.Text.Json.JsonSerializer.Serialize(new
                            {
                                profile.Status,
                                profile.TrialEndsAtUtc,
                                HoursLeft = Math.Round(hoursLeft, 1),
                                Recipient = company.Email,
                                Language = lang
                            }),
                            CreatedAtUtc = DateTime.UtcNow,
                        });
                        await db.SaveChangesAsync(ct);
                        sent++;
                        _logger.LogInformation(
                            "Trial reminder D-{Days} sent to company {CompanyId} ({Email}) — trial ends {TrialEnds:o}",
                            marker, profile.CompanyId, company.Email, profile.TrialEndsAtUtc.Value);
                    }
                    else
                    {
                        failed++;
                        _logger.LogWarning(
                            "Trial reminder D-{Days} FAILED for company {CompanyId} ({Email}): {Error}",
                            marker, profile.CompanyId, company.Email, result.Error);
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex,
                        "Trial reminder D-{Days} threw for company {CompanyId} ({Email})",
                        marker, profile.CompanyId, company.Email);
                }
            }

            _logger.LogInformation(
                "MessagingTrialReminder batch — total:{Total} sent:{Sent} failed:{Failed} skipped:{Skipped}",
                profiles.Count, sent, failed, skipped);
            return (sent, failed, skipped, profiles.Count);
        }
    }
}
