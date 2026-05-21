using Core.Enums.Messaging;
using Core.Enums.Appointment;
using Core.Models;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services.Integrations.SendGrid;
using Services;
using Services.Messaging;

namespace ControlApi.BackgroundJobs
{
    /// <summary>
    /// Sends Email reminders ~48h before each appointment occurrence (NORMAL + RECURRING).
    /// TZ-aware: Appointment.Start is local clock-time of TimeZoneId; converted to UTC before
    /// window comparisons. Honors RecurrenceExceptions (cancellations + OverrideStart/End reschedules).
    ///
    /// On first run after deploy, widens to [now, now+49h] to backfill occurrences whose 48h pickup
    /// window already elapsed.
    /// </summary>
    public class EmailAppointmentReminder48hHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailAppointmentReminder48hHostedService> _logger;
        private static readonly TimeSpan POLL = TimeSpan.FromMinutes(60);
        private readonly IBackgroundJobMonitorService _jobMonitor;

        public EmailAppointmentReminder48hHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<EmailAppointmentReminder48hHostedService> logger,
            IBackgroundJobMonitorService jobMonitor)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _jobMonitor = jobMonitor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("EmailAppointmentReminder48hHostedService started; polling every {Interval}", POLL);
            await _jobMonitor.EnsureDefaultsRegisteredAsync(stoppingToken);

            bool firstRun = true;
            while (!stoppingToken.IsCancellationRequested)
            {
                var nextRunUtc = DateTime.UtcNow.Add(POLL);
                var run = await _jobMonitor.MarkStartedAsync(
                    BackgroundJobKeys.EmailAppointmentReminder48h, "Email Reminder 48h", "Messaging", nextRunUtc, stoppingToken);
                try
                {
                    var (sent, failed, skipped, total) = await ProcessOnceAsync(firstRun, stoppingToken);
                    firstRun = false;
                    var summary = $"sent:{sent} failed:{failed} skipped:{skipped}";
                    await _jobMonitor.MarkSucceededAsync(run, summary, total, sent, failed, nextRunUtc, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "EmailAppointmentReminder48h loop error");
                    await _jobMonitor.MarkFailedAsync(run, ex, "Unexpected error in Email 48h reminder job.", nextPlannedRunAtUtc: nextRunUtc, ct: stoppingToken);
                }
                try { await Task.Delay(POLL, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task<(int sent, int failed, int skipped, int total)> ProcessOnceAsync(bool firstRun, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();
            var sendGrid = scope.ServiceProvider.GetRequiredService<ISendGridEmailSender>();

            var nowUtc = DateTime.UtcNow;
            // CATCH-UP MODE: window is always [now, now+49h], not [now+47h, now+49h].
            // Same rationale as SMS24h above — if a previous tick missed an occurrence, the
            // next tick still picks it up. Dedup (±60min on OccurrenceStartUtc) prevents
            // duplicates for occurrences with an existing Sent/Pending log.
            var windowStart = nowUtc;
            var windowEnd = nowUtc.AddHours(49);

            var occurrences = await CollectOccurrencesAsync(db, windowStart, windowEnd, ct);
            if (occurrences.Count == 0) return (0, 0, 0, 0);

            var apptIds = occurrences.Select(o => o.AppointmentId).Distinct().ToList();
            var existing = await db.AppointmentMessageLogs.AsNoTracking()
                .Where(l => apptIds.Contains(l.AppointmentId) &&
                            l.Kind == AppointmentMessageKind.ReminderEmail48h &&
                            l.Channel == AppointmentMessageChannel.Email &&
                            (l.Status == AppointmentMessageStatus.Sent || l.Status == AppointmentMessageStatus.Pending))
                .Select(l => new { l.AppointmentId, l.OccurrenceStartUtc })
                .ToListAsync(ct);

            int sent = 0, failed = 0, skipped = 0;

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
                    .Select(c => new { c.Name, c.Email, c.Language, c.ReceiveEmail })
                    .FirstOrDefaultAsync(ct);
                if (customer == null || string.IsNullOrWhiteSpace(customer.Email) || customer.ReceiveEmail == false)
                {
                    skipped++; continue;
                }

                var company = await db.Companies.AsNoTracking()
                    .Where(c => c.Id == occ.CompanyId)
                    .Select(c => new { c.Id, c.Name, c.Email })
                    .FirstOrDefaultAsync(ct);

                var lang = (customer.Language ?? "en").ToLowerInvariant();
                var customerName = customer.Name ?? "customer";
                var companyName = company?.Name ?? "MaidsFlow";
                var localTime = occ.OriginalStartLocal.ToString("dddd, MMMM d, yyyy 'at' h:mm tt");

                var (subject, html) = BuildEmail(lang, customerName, companyName, localTime);

                var log = new AppointmentMessageLog
                {
                    AppointmentId = occ.AppointmentId,
                    SeriesId = occ.SeriesId,
                    OccurrenceStartUtc = occ.StartUtc,
                    OccurrenceEndUtc = occ.EndUtc,
                    Kind = AppointmentMessageKind.ReminderEmail48h,
                    Channel = AppointmentMessageChannel.Email,
                    Status = AppointmentMessageStatus.Pending,
                    ScheduledForUtc = nowUtc,
                    Attempt = 1,
                    RecipientEmail = customer.Email,
                    Subject = subject,
                    BodyText = html,
                    TemplateKey = "email.appointmentReminder48h",
                    Provider = "SendGrid",
                };
                db.AppointmentMessageLogs.Add(log);
                await db.SaveChangesAsync(ct);

                try
                {
                    var result = await sendGrid.SendAsync(new SendGridEmailMessage(
                        ToEmail: customer.Email!,
                        Subject: subject,
                        PlainText: System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty),
                        Html: html,
                        ToName: customerName
                    ), ct);

                    if (result.Ok)
                    {
                        log.Status = AppointmentMessageStatus.Sent;
                        log.SentAtUtc = DateTime.UtcNow;
                        log.ProviderMessageId = result.StatusCode.ToString();
                        log.ProviderStatus = "queued";
                        await db.SaveChangesAsync(ct);
                        sent++;
                    }
                    else
                    {
                        log.Status = AppointmentMessageStatus.Failed;
                        log.LastError = result.Error;
                        log.LastErrorRaw = result.ResponseBody;
                        log.ScheduledForUtc = nowUtc.AddMinutes(15);
                        await db.SaveChangesAsync(ct);
                        failed++;
                    }
                }
                catch (Exception ex)
                {
                    log.Status = AppointmentMessageStatus.Failed;
                    log.LastError = ex.Message;
                    log.LastErrorRaw = ex.ToString();
                    log.ScheduledForUtc = nowUtc.AddMinutes(15);
                    await db.SaveChangesAsync(ct);
                    _logger.LogError(ex, "EmailAppointmentReminder48h error for appt {Id} occ {Start}", occ.AppointmentId, occ.StartUtc);
                    failed++;
                }
            }

            _logger.LogInformation(
                "EmailAppointmentReminder48h batch — total:{Total} sent:{Sent} failed:{Failed} skipped:{Skipped} (firstRun={FirstRun})",
                occurrences.Count, sent, failed, skipped, firstRun);
            return (sent, failed, skipped, occurrences.Count);
        }

        private static (string subject, string html) BuildEmail(string lang, string customerName, string companyName, string localTime)
        {
            return lang switch
            {
                "pt-br" or "pt" => (
                    $"Lembrete: seu serviço com {companyName} é em 48 horas",
                    $"<p>Olá {customerName},</p><p>Este é um lembrete de que seu serviço com <strong>{companyName}</strong> está agendado para <strong>{localTime}</strong>.</p><p>Se precisar reagendar ou tiver dúvidas, entre em contato com {companyName} diretamente.</p><p>Obrigado!</p>"
                ),
                "es" or "es-es" => (
                    $"Recordatorio: tu servicio con {companyName} es en 48 horas",
                    $"<p>Hola {customerName},</p><p>Este es un recordatorio de que tu servicio con <strong>{companyName}</strong> está programado para <strong>{localTime}</strong>.</p><p>Si necesitas reprogramar o tienes preguntas, comunícate con {companyName}.</p><p>¡Gracias!</p>"
                ),
                "fr" or "fr-fr" => (
                    $"Rappel : votre service avec {companyName} dans 48 heures",
                    $"<p>Bonjour {customerName},</p><p>Ceci est un rappel pour votre service avec <strong>{companyName}</strong> prévu le <strong>{localTime}</strong>.</p><p>Si vous devez reprogramer ou avez des questions, contactez {companyName} directement.</p><p>Merci !</p>"
                ),
                _ => (
                    $"Reminder: your appointment with {companyName} is in 48 hours",
                    $"<p>Hi {customerName},</p><p>This is a reminder that your appointment with <strong>{companyName}</strong> is scheduled for <strong>{localTime}</strong>.</p><p>If you need to reschedule or have questions, please reach out to {companyName} directly.</p><p>Thank you!</p>"
                )
            };
        }

        // ----- Occurrence collection (TZ-aware) -----

        private record TargetOccurrence(
            int AppointmentId, int CompanyId, int? CustomerId, Guid? SeriesId,
            DateTime StartUtc, DateTime EndUtc, DateTime OriginalStartLocal);

        private static async Task<List<TargetOccurrence>> CollectOccurrencesAsync(
            DbContextClass db, DateTime windowStartUtc, DateTime windowEndUtc, CancellationToken ct)
        {
            var list = new List<TargetOccurrence>();
            var paddedStart = windowStartUtc.AddDays(-2);
            var paddedEnd = windowEndUtc.AddDays(2);

            // 1) Normal (non-recurring) — TZ derived from customer's phone + state, falling back to anchor.TimeZoneId
            var normals = await (
                from a in db.Appointments.AsNoTracking()
                join c in db.Customers.AsNoTracking() on a.CustomerId equals c.Id into gc
                from c in gc.DefaultIfEmpty()
                where !a.IsRecurring && a.Status == AppointmentStatus.Scheduled &&
                      a.Start >= paddedStart && a.Start <= paddedEnd
                select new { a.Id, a.CompanyId, a.CustomerId, a.Start, a.End, a.TimeZoneId,
                             CustPhone = c != null ? c.Phone : null,
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

            // 2) Recurring anchors
            var anchors = await db.Appointments.AsNoTracking()
                .Where(a => a.IsRecurring && a.Status == AppointmentStatus.Scheduled &&
                            a.SeriesId != null && !string.IsNullOrWhiteSpace(a.RecurrenceRule) &&
                            a.Start <= paddedEnd &&
                            (a.RecurrenceEnd == null || a.RecurrenceEnd >= paddedStart))
                .ToListAsync(ct);

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
                    .Select(c => new { c.Id, c.Phone, c.State })
                    .ToListAsync(ct);
                foreach (var r in rows) custLookup[r.Id] = (r.Phone, r.State);
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
