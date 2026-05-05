using Core.Enums.Messaging;
using Core.Models;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services;
using Services.Messaging;

namespace ControlApi.BackgroundJobs
{
    /// <summary>
    /// Sends SMS reminders ~24h before each appointment occurrence (NORMAL + RECURRING).
    /// Replaces the old n8n workflow.
    /// </summary>
    public class SmsAppointmentReminder24hHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SmsAppointmentReminder24hHostedService> _logger;
        private static readonly TimeSpan POLL = TimeSpan.FromMinutes(30);

        private readonly IBackgroundJobMonitorService _jobMonitor;

        public SmsAppointmentReminder24hHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<SmsAppointmentReminder24hHostedService> logger,
            IBackgroundJobMonitorService jobMonitor)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _jobMonitor = jobMonitor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SmsAppointmentReminder24hHostedService started; polling every {Interval}", POLL);
            await _jobMonitor.EnsureDefaultsRegisteredAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var nextRunUtc = DateTime.UtcNow.Add(POLL);
                var run = await _jobMonitor.MarkStartedAsync(
                    BackgroundJobKeys.SmsAppointmentReminder24h, "SMS Reminder 24h", "Messaging", nextRunUtc, stoppingToken);
                try
                {
                    var (sent, blocked, failed, skipped, total) = await ProcessOnceAsync(stoppingToken);
                    var summary = $"sent:{sent} blocked:{blocked} failed:{failed} skipped:{skipped}";
                    await _jobMonitor.MarkSucceededAsync(run, summary, total, sent, failed, nextRunUtc, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SmsAppointmentReminder24h loop error");
                    await _jobMonitor.MarkFailedAsync(run, ex, "Unexpected error in SMS 24h reminder job.", nextPlannedRunAtUtc: nextRunUtc, ct: stoppingToken);
                }
                try { await Task.Delay(POLL, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task<(int sent, int blocked, int failed, int skipped, int total)> ProcessOnceAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();
            var dispatch = scope.ServiceProvider.GetRequiredService<ISmsDispatchService>();

            var nowUtc = DateTime.UtcNow;
            var windowStart = nowUtc.AddHours(23);
            var windowEnd = nowUtc.AddHours(25);

            var occurrences = await CollectOccurrencesAsync(db, windowStart, windowEnd, ct);
            if (occurrences.Count == 0) return (0, 0, 0, 0, 0);

            // Pre-load existing 24h SMS logs to avoid duplicates per (AppointmentId, OccurrenceStartUtc)
            var apptIds = occurrences.Select(o => o.AppointmentId).Distinct().ToList();
            var existing = await db.AppointmentMessageLogs.AsNoTracking()
                .Where(l => apptIds.Contains(l.AppointmentId) &&
                            l.Kind == AppointmentMessageKind.ConfirmationSms24h &&
                            l.Channel == AppointmentMessageChannel.Sms &&
                            (l.Status == AppointmentMessageStatus.Sent ||
                             l.Status == AppointmentMessageStatus.Pending))
                .Select(l => new { l.AppointmentId, l.OccurrenceStartUtc })
                .ToListAsync(ct);

            int sent = 0, blocked = 0, failed = 0, skipped = 0;

            foreach (var occ in occurrences)
            {
                if (ct.IsCancellationRequested) break;

                // Tight dedup: a previous log only counts if its OccurrenceStartUtc is within
                // ±60 min of the CURRENT occurrence start. Logs with NULL OccurrenceStartUtc
                // (legacy data) or with timestamps far away (appointment was rescheduled) are
                // NOT considered "already sent" — we want to dispatch a fresh log for the new time.
                bool already = existing.Any(e =>
                    e.AppointmentId == occ.AppointmentId &&
                    e.OccurrenceStartUtc.HasValue &&
                    Math.Abs((e.OccurrenceStartUtc.Value - occ.Start).TotalMinutes) <= 60);
                if (already) { skipped++; continue; }

                if (occ.CustomerId == null) { skipped++; continue; }
                var customer = await db.Customers.AsNoTracking()
                    .Where(c => c.Id == occ.CustomerId.Value)
                    .Select(c => new { c.Id, c.Name, c.Phone, c.Language })
                    .FirstOrDefaultAsync(ct);
                var to = customer?.Phone;
                if (string.IsNullOrWhiteSpace(to)) { skipped++; continue; }

                var company = await db.Companies.AsNoTracking()
                    .Where(c => c.Id == occ.CompanyId)
                    .Select(c => new { c.Id, c.Name })
                    .FirstOrDefaultAsync(ct);

                var lang = (customer?.Language ?? "en").ToLowerInvariant();
                var customerName = customer?.Name ?? "customer";
                var companyName = company?.Name ?? "MaidsFlow";
                var localTime = occ.Start.ToString("MMM dd, h:mm tt");

                string body = lang switch
                {
                    "pt-br" or "pt" => $"Oi {customerName}! Lembrete: seu serviço com {companyName} está agendado para {localTime}. Responda STOP para cancelar.",
                    "es" or "es-es" => $"Hola {customerName}! Recordatorio: tu servicio con {companyName} está programado para {localTime}. Responde STOP para cancelar.",
                    "fr" or "fr-fr" => $"Bonjour {customerName}! Rappel: votre service avec {companyName} est prévu pour {localTime}. Répondez STOP pour annuler.",
                    _ => $"Hi {customerName}! Reminder: your appointment with {companyName} is scheduled for {localTime}. Reply STOP to opt out.",
                };

                try
                {
                    var result = await dispatch.DispatchAsync(new SmsDispatchRequest
                    {
                        CompanyId = occ.CompanyId,
                        AppointmentId = occ.AppointmentId,
                        SeriesId = occ.SeriesId,
                        OccurrenceStartUtc = occ.Start,
                        OccurrenceEndUtc = occ.End,
                        Kind = AppointmentMessageKind.ConfirmationSms24h,
                        ToPhoneE164 = to!,
                        Body = body,
                        TemplateKey = "sms.appointmentReminder24h",
                    }, ct);

                    switch (result.Outcome)
                    {
                        case SmsDispatchOutcome.Sent: sent++; break;
                        case SmsDispatchOutcome.Blocked: blocked++; break;
                        default: failed++; break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SmsAppointmentReminder24h dispatch error for appt {Id} occ {Start}", occ.AppointmentId, occ.Start);
                    failed++;
                }
            }

            _logger.LogInformation(
                "SmsAppointmentReminder24h batch — total:{Total} sent:{Sent} blocked:{Blocked} failed:{Failed} skipped:{Skipped}",
                occurrences.Count, sent, blocked, failed, skipped);
            return (sent, blocked, failed, skipped, occurrences.Count);
        }

        // ----- Occurrence collection (NORMAL + RECURRING) -----

        private record TargetOccurrence(int AppointmentId, int CompanyId, int? CustomerId, Guid? SeriesId, DateTime Start, DateTime End);

        private static async Task<List<TargetOccurrence>> CollectOccurrencesAsync(
            DbContextClass db, DateTime windowStart, DateTime windowEnd, CancellationToken ct)
        {
            var list = new List<TargetOccurrence>();

            // 1) Normal (non-recurring) appointments
            var normals = await db.Appointments.AsNoTracking()
                .Where(a => !a.IsRecurring &&
                            a.Status == 0 &&
                            a.Start >= windowStart && a.Start <= windowEnd)
                .Select(a => new { a.Id, a.CompanyId, a.CustomerId, a.Start, a.End })
                .ToListAsync(ct);

            list.AddRange(normals.Select(n =>
                new TargetOccurrence(n.Id, n.CompanyId, n.CustomerId, null, n.Start, n.End)));

            // 2) Recurring anchors that have an occurrence in this window
            var anchors = await db.Appointments.AsNoTracking()
                .Where(a => a.IsRecurring &&
                            a.Status == 0 &&
                            a.SeriesId != null &&
                            !string.IsNullOrWhiteSpace(a.RecurrenceRule) &&
                            a.Start <= windowEnd &&
                            (a.RecurrenceEnd == null || a.RecurrenceEnd >= windowStart))
                .ToListAsync(ct);

            // 2a) Pull exceptions for these series that fall in window (cancellations + overrides)
            var seriesIds = anchors.Select(a => a.SeriesId!.Value).Distinct().ToList();
            var exceptions = await db.Set<AppointmentRecurrenceException>().AsNoTracking()
                .Where(e => seriesIds.Contains(e.SeriesId) &&
                            e.OccurrenceStart >= windowStart.AddDays(-1) &&
                            e.OccurrenceStart <= windowEnd.AddDays(1))
                .Select(e => new { e.SeriesId, e.OccurrenceStart, e.IsCancelled })
                .ToListAsync(ct);

            foreach (var anchor in anchors)
            {
                foreach (var occ in RecurrenceEnumerator.ExpandInWindow(anchor, windowStart, windowEnd))
                {
                    // Skip if this occurrence has a cancellation exception
                    bool isCancelled = exceptions.Any(e =>
                        e.SeriesId == anchor.SeriesId!.Value &&
                        e.IsCancelled &&
                        e.OccurrenceStart == occ.Start);
                    if (isCancelled) continue;

                    list.Add(new TargetOccurrence(
                        anchor.Id, anchor.CompanyId, anchor.CustomerId,
                        anchor.SeriesId, occ.Start, occ.End));
                }
            }

            return list;
        }
    }
}
