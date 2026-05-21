using Core.Enums.Messaging;
using Core.Enums.Appointment;
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
    /// TZ-aware: Appointment.Start is stored as local clock-time of TimeZoneId; all window math
    /// is done in UTC after converting local→UTC via the appointment's tz.
    ///
    /// On first execution after deploy, runs a wider backfill window so occurrences whose
    /// normal pickup slot already elapsed get caught.
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

            bool firstRun = true;
            while (!stoppingToken.IsCancellationRequested)
            {
                var nextRunUtc = DateTime.UtcNow.Add(POLL);
                var run = await _jobMonitor.MarkStartedAsync(
                    BackgroundJobKeys.SmsAppointmentReminder24h, "SMS Reminder 24h", "Messaging", nextRunUtc, stoppingToken);
                try
                {
                    var (sent, blocked, failed, skipped, total) = await ProcessOnceAsync(firstRun, stoppingToken);
                    firstRun = false;
                    var summary = $"sent:{sent} blocked:{blocked} failed:{failed} skipped:{skipped}";
                    await _jobMonitor.MarkSucceededAsync(run, summary, total, sent, failed, nextRunUtc, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SmsAppointmentReminder24h iteration failed");
                    await _jobMonitor.MarkFailedAsync(run, ex, "Unexpected error in SMS 24h reminder job.", nextPlannedRunAtUtc: nextRunUtc, ct: stoppingToken);
                }
                try { await Task.Delay(POLL, stoppingToken); } catch { }
            }
        }

        private async Task<(int sent, int blocked, int failed, int skipped, int total)> ProcessOnceAsync(bool firstRun, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();
            var dispatch = scope.ServiceProvider.GetRequiredService<ISmsDispatchService>();

            var nowUtc = DateTime.UtcNow;
            // CATCH-UP MODE: window is always [now, now+25h], not [now+23h, now+25h].
            // Why: if an appointment is in the future but a prior tick missed its 24h pickup
            // (process restart, deploy gap, TZ misconfig, etc.) we'd never send the SMS at all.
            // With this window, ANY future occurrence within 25h that has no log gets a fresh
            // send on the very next tick — no message left behind.
            // Dedup (±60min on OccurrenceStartUtc) still prevents duplicates for occurrences
            // that already have a Sent/Pending log.
            var windowStart = nowUtc;
            var windowEnd = nowUtc.AddHours(25);

            var occurrences = await CollectOccurrencesAsync(db, windowStart, windowEnd, ct);
            if (occurrences.Count == 0) return (0, 0, 0, 0, 0);

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

                bool already = existing.Any(e =>
                    e.AppointmentId == occ.AppointmentId &&
                    e.OccurrenceStartUtc.HasValue &&
                    Math.Abs((e.OccurrenceStartUtc.Value - occ.StartUtc).TotalMinutes) <= 60);
                if (already) { skipped++; continue; }

                if (occ.CustomerId == null) { skipped++; continue; }
                var customer = await db.Customers.AsNoTracking()
                    .Where(c => c.Id == occ.CustomerId.Value)
                    .Select(c => new { c.Id, c.Name, c.Phone, c.Phone2, c.Language, c.ReceiveSms })
                    .FirstOrDefaultAsync(ct);
                if (customer == null) { skipped++; continue; }
                if (!customer.ReceiveSms) { skipped++; continue; }    // <-- opt-out check
                // Phone fallback: try primary, fall back to secondary if primary is missing
                var to = !string.IsNullOrWhiteSpace(customer.Phone) ? customer.Phone : customer.Phone2;
                if (string.IsNullOrWhiteSpace(to)) { skipped++; continue; }

                var company = await db.Companies.AsNoTracking()
                    .Where(c => c.Id == occ.CompanyId)
                    .Select(c => new { c.Id, c.Name })
                    .FirstOrDefaultAsync(ct);

                var lang = (customer.Language ?? "en").ToLowerInvariant();
                var customerName = customer.Name ?? "customer";
                var companyName = company?.Name ?? "MaidsFlow";
                // Render time in the appointment's local tz (clock-time the customer sees).
                var localTime = occ.OriginalStartLocal.ToString("MMM dd, h:mm tt");

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
                        OccurrenceStartUtc = occ.StartUtc,
                        OccurrenceEndUtc = occ.EndUtc,
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
                    _logger.LogError(ex, "SmsAppointmentReminder24h dispatch error for appt {Id} occ {Start}", occ.AppointmentId, occ.StartUtc);
                    failed++;
                }
            }

            _logger.LogInformation(
                "SmsAppointmentReminder24h batch — total:{Total} sent:{Sent} blocked:{Blocked} failed:{Failed} skipped:{Skipped} (firstRun={FirstRun})",
                occurrences.Count, sent, blocked, failed, skipped, firstRun);
            return (sent, blocked, failed, skipped, occurrences.Count);
        }

        // ----- Occurrence collection (NORMAL + RECURRING, TZ-aware) -----

        private record TargetOccurrence(
            int AppointmentId, int CompanyId, int? CustomerId, Guid? SeriesId,
            DateTime StartUtc, DateTime EndUtc, DateTime OriginalStartLocal);

        private static async Task<List<TargetOccurrence>> CollectOccurrencesAsync(
            DbContextClass db, DateTime windowStartUtc, DateTime windowEndUtc, CancellationToken ct)
        {
            var list = new List<TargetOccurrence>();

            // --- 1) NORMAL (non-recurring) appointments ---
            // We fetch a wide candidate range and filter precisely after tz conversion (per-appt tz).
            var paddedStart = windowStartUtc.AddDays(-2);
            var paddedEnd = windowEndUtc.AddDays(2);
            var normals = await (
                from a in db.Appointments.AsNoTracking()
                join c in db.Customers.AsNoTracking() on a.CustomerId equals c.Id into gc
                from c in gc.DefaultIfEmpty()
                where !a.IsRecurring && a.Status == AppointmentStatus.Scheduled &&
                      a.Start >= paddedStart && a.Start <= paddedEnd
                select new { a.Id, a.CompanyId, a.CustomerId, a.Start, a.End, a.TimeZoneId,
                             CustPhone = c != null ? (!string.IsNullOrWhiteSpace(c.Phone) ? c.Phone : c.Phone2) : null,
                             CustState = c != null ? c.State : null }
            ).ToListAsync(ct);

            foreach (var n in normals)
            {
                var tz = MessagingTimeZoneResolver.Resolve(n.CustPhone, n.CustState, n.TimeZoneId);
                var startUtc = MessagingTimeZoneResolver.LocalToUtc(n.Start, tz);
                if (startUtc < windowStartUtc || startUtc > windowEndUtc) continue;
                var endUtc = MessagingTimeZoneResolver.LocalToUtc(n.End, tz);
                list.Add(new TargetOccurrence(n.Id, n.CompanyId, n.CustomerId, null,
                    startUtc, endUtc, DateTime.SpecifyKind(n.Start, DateTimeKind.Unspecified)));
            }

            // --- 2) RECURRING anchors that may have an occurrence in this window ---
            var anchors = await db.Appointments.AsNoTracking()
                .Where(a => a.IsRecurring &&
                            a.Status == AppointmentStatus.Scheduled &&
                            a.SeriesId != null &&
                            !string.IsNullOrWhiteSpace(a.RecurrenceRule) &&
                            a.Start <= paddedEnd &&
                            (a.RecurrenceEnd == null || a.RecurrenceEnd >= paddedStart))
                .ToListAsync(ct);

            // Pull all relevant exceptions in one query
            var seriesIds = anchors.Select(a => a.SeriesId!.Value).Distinct().ToList();
            List<AppointmentRecurrenceException> exceptions = new();
            if (seriesIds.Count > 0)
            {
                exceptions = await db.Set<AppointmentRecurrenceException>().AsNoTracking()
                    .Where(e => seriesIds.Contains(e.SeriesId))
                    .ToListAsync(ct);
            }
            var exBySeries = exceptions.GroupBy(e => e.SeriesId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<AppointmentRecurrenceException>)g.ToList());

            // Pre-load customer phone+state for all anchored series in one query
            var anchorCustIds = anchors.Where(a => a.CustomerId.HasValue).Select(a => a.CustomerId!.Value).Distinct().ToList();
            var custLookup = new Dictionary<int, (string? Phone, string? State)>();
            if (anchorCustIds.Count > 0)
            {
                var rows = await db.Customers.AsNoTracking()
                    .Where(c => anchorCustIds.Contains(c.Id))
                    .Select(c => new { c.Id, c.Phone, c.Phone2, c.State })
                    .ToListAsync(ct);
                foreach (var r in rows)
                {
                    var bestPhone = !string.IsNullOrWhiteSpace(r.Phone) ? r.Phone : r.Phone2;
                    custLookup[r.Id] = (bestPhone, r.State);
                }
            }

            foreach (var anchor in anchors)
            {
                string? cphone = null, cstate = null;
                if (anchor.CustomerId.HasValue && custLookup.TryGetValue(anchor.CustomerId.Value, out var cc))
                {
                    cphone = cc.Phone; cstate = cc.State;
                }
                var tz = MessagingTimeZoneResolver.Resolve(cphone, cstate, anchor.TimeZoneId);
                exBySeries.TryGetValue(anchor.SeriesId!.Value, out var seriesEx);

                foreach (var occ in RecurrenceEnumerator.ExpandInWindow(anchor, windowStartUtc, windowEndUtc, tz, seriesEx))
                {
                    list.Add(new TargetOccurrence(
                        anchor.Id, anchor.CompanyId, anchor.CustomerId,
                        anchor.SeriesId, occ.StartUtc, occ.EndUtc, occ.OriginalStartLocal));
                }
            }

            return list;
        }
    }
}
