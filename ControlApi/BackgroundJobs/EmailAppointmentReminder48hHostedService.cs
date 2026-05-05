using Core.Enums.Messaging;
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
    /// Replaces the old n8n workflow "MaidsFlow — Email (48h) only".
    ///
    /// Idempotent per (AppointmentId, OccurrenceStartUtc, Kind=ReminderEmail48h).
    /// Sends via SendGrid; updates AppointmentMessageLog.
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

            while (!stoppingToken.IsCancellationRequested)
            {
                var nextRunUtc = DateTime.UtcNow.Add(POLL);
                var run = await _jobMonitor.MarkStartedAsync(
                    BackgroundJobKeys.EmailAppointmentReminder48h, "Email Reminder 48h", "Messaging", nextRunUtc, stoppingToken);
                try
                {
                    var (sent, failed, skipped, total) = await ProcessOnceAsync(stoppingToken);
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

        private async Task<(int sent, int failed, int skipped, int total)> ProcessOnceAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();
            var sendGrid = scope.ServiceProvider.GetRequiredService<ISendGridEmailSender>();

            var nowUtc = DateTime.UtcNow;
            var windowStart = nowUtc.AddHours(47);
            var windowEnd = nowUtc.AddHours(49);

            var occurrences = await CollectOccurrencesAsync(db, windowStart, windowEnd, ct);
            if (occurrences.Count == 0) return (0, 0, 0, 0);

            // Pre-load existing 48h email logs
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
                var localTime = occ.Start.ToString("dddd, MMMM d, yyyy 'at' h:mm tt");

                var (subject, html) = BuildEmail(lang, customerName, companyName, localTime);

                // Create log row first
                var log = new AppointmentMessageLog
                {
                    AppointmentId = occ.AppointmentId,
                    SeriesId = occ.SeriesId,
                    OccurrenceStartUtc = occ.Start,
                    OccurrenceEndUtc = occ.End,
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
                        log.ScheduledForUtc = nowUtc.AddMinutes(15); // simple email retry slot
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
                    _logger.LogError(ex, "EmailAppointmentReminder48h error for appt {Id} occ {Start}", occ.AppointmentId, occ.Start);
                    failed++;
                }
            }

            _logger.LogInformation(
                "EmailAppointmentReminder48h batch — total:{Total} sent:{Sent} failed:{Failed} skipped:{Skipped}",
                occurrences.Count, sent, failed, skipped);
            return (sent, failed, skipped, occurrences.Count);
        }

        private static (string subject, string html) BuildEmail(string lang, string customerName, string companyName, string localTime)
        {
            switch (lang)
            {
                case "pt-br":
                case "pt":
                    return (
                        $"Lembrete: seu serviço com {companyName} é em 48 horas",
                        $"<p>Olá {customerName},</p><p>Este é um lembrete de que seu serviço com <strong>{companyName}</strong> está agendado para <strong>{localTime}</strong>.</p><p>Se precisar reagendar ou tiver dúvidas, entre em contato com {companyName} diretamente.</p><p>Obrigado!</p>"
                    );
                case "es":
                case "es-es":
                    return (
                        $"Recordatorio: tu servicio con {companyName} es en 48 horas",
                        $"<p>Hola {customerName},</p><p>Este es un recordatorio de que tu servicio con <strong>{companyName}</strong> está programado para <strong>{localTime}</strong>.</p><p>Si necesitas reprogramar o tienes preguntas, comunícate con {companyName}.</p><p>¡Gracias!</p>"
                    );
                case "fr":
                case "fr-fr":
                    return (
                        $"Rappel : votre service avec {companyName} dans 48 heures",
                        $"<p>Bonjour {customerName},</p><p>Ceci est un rappel pour votre service avec <strong>{companyName}</strong> prévu le <strong>{localTime}</strong>.</p><p>Si vous devez reprogrammer ou avez des questions, contactez {companyName} directement.</p><p>Merci !</p>"
                    );
                default:
                    return (
                        $"Reminder: your appointment with {companyName} is in 48 hours",
                        $"<p>Hi {customerName},</p><p>This is a reminder that your appointment with <strong>{companyName}</strong> is scheduled for <strong>{localTime}</strong>.</p><p>If you need to reschedule or have questions, please reach out to {companyName} directly.</p><p>Thank you!</p>"
                    );
            }
        }

        private record TargetOccurrence(int AppointmentId, int CompanyId, int? CustomerId, Guid? SeriesId, DateTime Start, DateTime End);

        private static async Task<List<TargetOccurrence>> CollectOccurrencesAsync(
            DbContextClass db, DateTime windowStart, DateTime windowEnd, CancellationToken ct)
        {
            var list = new List<TargetOccurrence>();

            var normals = await db.Appointments.AsNoTracking()
                .Where(a => !a.IsRecurring && a.Status == 0 && a.Start >= windowStart && a.Start <= windowEnd)
                .Select(a => new { a.Id, a.CompanyId, a.CustomerId, a.Start, a.End })
                .ToListAsync(ct);
            list.AddRange(normals.Select(n => new TargetOccurrence(n.Id, n.CompanyId, n.CustomerId, null, n.Start, n.End)));

            var anchors = await db.Appointments.AsNoTracking()
                .Where(a => a.IsRecurring && a.Status == 0 &&
                            a.SeriesId != null && !string.IsNullOrWhiteSpace(a.RecurrenceRule) &&
                            a.Start <= windowEnd &&
                            (a.RecurrenceEnd == null || a.RecurrenceEnd >= windowStart))
                .ToListAsync(ct);

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
                    bool isCancelled = exceptions.Any(e =>
                        e.SeriesId == anchor.SeriesId!.Value &&
                        e.IsCancelled &&
                        e.OccurrenceStart == occ.Start);
                    if (isCancelled) continue;
                    list.Add(new TargetOccurrence(anchor.Id, anchor.CompanyId, anchor.CustomerId, anchor.SeriesId, occ.Start, occ.End));
                }
            }

            return list;
        }
    }
}
