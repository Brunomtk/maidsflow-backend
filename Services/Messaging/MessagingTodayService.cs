using Core.Enums.Appointment;
using Core.Enums.Messaging;
using Core.Models;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Integrations.SendGrid;

namespace Services.Messaging;

/// <summary>
/// Admin-only "today" dispatcher: lists every reminder due to be sent **today**
/// (24h SMS + 48h Email, normal + recurring) and lets an admin force-send all
/// of them at once instead of waiting for the next hosted-service tick.
/// </summary>
public interface IMessagingTodayService
{
    Task<TodayPlanDto> ListTodayAsync(CancellationToken ct);
    Task<TodayRunResultDto> RunTodayAsync(CancellationToken ct);
}

public sealed class TodayItemDto
{
    public int AppointmentId { get; set; }
    public Guid? SeriesId { get; set; }
    public DateTime OccurrenceStartUtc { get; set; }
    public DateTime OccurrenceEndUtc { get; set; }
    public int CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? RecipientPhone { get; set; }
    public string? RecipientEmail { get; set; }
    public string Kind { get; set; } = "";        // "ConfirmationSms24h" | "ReminderEmail48h"
    public string Channel { get; set; } = "";     // "Sms" | "Email"
    public string Status { get; set; } = "Pending"; // "Pending" | "Sent" | "Failed"
    public DateTime? SentAtUtc { get; set; }
    public DateTime ScheduledForUtc { get; set; }
}

public sealed class TodayPlanDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public int Total { get; set; }
    public int AlreadySent { get; set; }
    public int Pending { get; set; }
    public List<TodayItemDto> Items { get; set; } = new();
}

public sealed class TodayRunResultDto
{
    public int Sent { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public int Total { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class MessagingTodayService : IMessagingTodayService
{
    private readonly DbContextClass _db;
    private readonly ISmsDispatchService _smsDispatch;
    private readonly ISendGridEmailSender _emailSender;
    private readonly ILogger<MessagingTodayService> _logger;

    public MessagingTodayService(
        DbContextClass db,
        ISmsDispatchService smsDispatch,
        ISendGridEmailSender emailSender,
        ILogger<MessagingTodayService> logger)
    {
        _db = db;
        _smsDispatch = smsDispatch;
        _emailSender = emailSender;
        _logger = logger;
    }

    // ----------------------------------------------------------------------
    // PLAN — what's scheduled for the rest of "today"
    // ----------------------------------------------------------------------

    public async Task<TodayPlanDto> ListTodayAsync(CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;
        // The window we actually look at — covers what is due within the next ~49h
        // (48h email reminders + buffer). We don't clip to "end of day" because reminders
        // for tomorrow are obviously inside this window too.
        var lookaheadEndUtc = nowUtc.AddHours(49);

        // We dispatch:
        //   24h SMS     → for any appointment whose Start ∈ [now+1min, now+25h]   (so reminders due within 24h)
        //   48h Email   → for any appointment whose Start ∈ [now+1min, now+49h]
        // Both windows trimmed to "today" + the next ~2 days so the admin sees the *imminent* schedule.
        var smsWindowEnd  = nowUtc.AddHours(25);
        var emailWindowEnd = nowUtc.AddHours(49);

        var smsOccurrences   = await CollectOccurrencesAsync(nowUtc, smsWindowEnd, ct);
        var emailOccurrences = await CollectOccurrencesAsync(nowUtc, emailWindowEnd, ct);

        var apptIds = smsOccurrences.Select(o => o.AppointmentId)
            .Concat(emailOccurrences.Select(o => o.AppointmentId))
            .Distinct()
            .ToList();

        var existingLogs = await _db.AppointmentMessageLogs.AsNoTracking()
            .Where(l => apptIds.Contains(l.AppointmentId) &&
                        (l.Kind == AppointmentMessageKind.ConfirmationSms24h ||
                         l.Kind == AppointmentMessageKind.ReminderEmail48h))
            .Select(l => new { l.AppointmentId, l.OccurrenceStartUtc, l.Kind, l.Channel, l.Status, l.SentAtUtc })
            .ToListAsync(ct);

        var customerIds = smsOccurrences.Concat(emailOccurrences)
            .Where(o => o.CustomerId.HasValue).Select(o => o.CustomerId!.Value)
            .Distinct().ToList();
        var customers = await _db.Customers.AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        var companyIds = smsOccurrences.Concat(emailOccurrences).Select(o => o.CompanyId).Distinct().ToList();
        var companies = await _db.Companies.AsNoTracking()
            .Where(c => companyIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var items = new List<TodayItemDto>();

        foreach (var o in smsOccurrences)
        {
            customers.TryGetValue(o.CustomerId ?? -1, out var cust);
            companies.TryGetValue(o.CompanyId, out var compName);
            var match = existingLogs.FirstOrDefault(l =>
                l.AppointmentId == o.AppointmentId &&
                l.Kind == AppointmentMessageKind.ConfirmationSms24h &&
                l.Channel == AppointmentMessageChannel.Sms &&
                (l.OccurrenceStartUtc == null || l.OccurrenceStartUtc == o.Start));

            items.Add(new TodayItemDto
            {
                AppointmentId = o.AppointmentId,
                SeriesId = o.SeriesId,
                OccurrenceStartUtc = o.Start,
                OccurrenceEndUtc = o.End,
                CompanyId = o.CompanyId,
                CompanyName = compName,
                CustomerId = o.CustomerId,
                CustomerName = cust?.Name,
                RecipientPhone = cust?.Phone,
                Kind = nameof(AppointmentMessageKind.ConfirmationSms24h),
                Channel = "Sms",
                Status = match == null ? "Pending"
                         : match.Status == AppointmentMessageStatus.Sent ? "Sent"
                         : match.Status == AppointmentMessageStatus.Failed ? "Failed"
                         : "Pending",
                SentAtUtc = match?.SentAtUtc,
                ScheduledForUtc = o.Start.AddHours(-24),
            });
        }

        foreach (var o in emailOccurrences)
        {
            customers.TryGetValue(o.CustomerId ?? -1, out var cust);
            companies.TryGetValue(o.CompanyId, out var compName);
            var match = existingLogs.FirstOrDefault(l =>
                l.AppointmentId == o.AppointmentId &&
                l.Kind == AppointmentMessageKind.ReminderEmail48h &&
                l.Channel == AppointmentMessageChannel.Email &&
                (l.OccurrenceStartUtc == null || l.OccurrenceStartUtc == o.Start));

            items.Add(new TodayItemDto
            {
                AppointmentId = o.AppointmentId,
                SeriesId = o.SeriesId,
                OccurrenceStartUtc = o.Start,
                OccurrenceEndUtc = o.End,
                CompanyId = o.CompanyId,
                CompanyName = compName,
                CustomerId = o.CustomerId,
                CustomerName = cust?.Name,
                RecipientEmail = cust?.Email,
                Kind = nameof(AppointmentMessageKind.ReminderEmail48h),
                Channel = "Email",
                Status = match == null ? "Pending"
                         : match.Status == AppointmentMessageStatus.Sent ? "Sent"
                         : match.Status == AppointmentMessageStatus.Failed ? "Failed"
                         : "Pending",
                SentAtUtc = match?.SentAtUtc,
                ScheduledForUtc = o.Start.AddHours(-48),
            });
        }

        items = items
            .OrderBy(x => x.ScheduledForUtc)
            .ThenBy(x => x.AppointmentId)
            .ToList();

        return new TodayPlanDto
        {
            FromUtc = nowUtc,
            ToUtc = lookaheadEndUtc,
            Total = items.Count,
            AlreadySent = items.Count(x => x.Status == "Sent"),
            Pending = items.Count(x => x.Status == "Pending"),
            Items = items,
        };
    }

    // ----------------------------------------------------------------------
    // RUN — fire every Pending dispatch right now
    // ----------------------------------------------------------------------

    public async Task<TodayRunResultDto> RunTodayAsync(CancellationToken ct)
    {
        var plan = await ListTodayAsync(ct);
        var pending = plan.Items.Where(i => i.Status == "Pending").ToList();

        int sent = 0, failed = 0, skipped = 0;
        var errors = new List<string>();

        foreach (var item in pending)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                // ----- Re-validate appointment is still Scheduled (status=0) -----
                // This guards against the case where the user listed the plan, then cancelled
                // the appointment, then clicked "Send all now". We don't want to send to a
                // cancelled / completed / no-show appointment.
                var stillScheduled = await _db.Appointments.AsNoTracking()
                    .Where(a => a.Id == item.AppointmentId)
                    .Select(a => new { a.Status, a.Start })
                    .FirstOrDefaultAsync(ct);
                if (stillScheduled == null || (int)stillScheduled.Status != 0)
                {
                    skipped++;
                    continue;
                }
                // Also defend against the appointment being moved while the panel was open:
                // if the current Start drifted more than 60 min from what we listed, skip too.
                if (Math.Abs((stillScheduled.Start - item.OccurrenceStartUtc).TotalMinutes) > 60)
                {
                    skipped++;
                    continue;
                }

                // ----- Resolve customer prefs + language ONCE per item -----
                bool? receiveSms = null, receiveEmail = null;
                string lang = "en";
                if (item.CustomerId.HasValue)
                {
                    var prefs = await _db.Customers.AsNoTracking()
                        .Where(c => c.Id == item.CustomerId.Value)
                        .Select(c => new { c.ReceiveSms, c.ReceiveEmail, c.Language })
                        .FirstOrDefaultAsync(ct);
                    if (prefs != null)
                    {
                        receiveSms   = prefs.ReceiveSms;
                        receiveEmail = prefs.ReceiveEmail;
                        lang = (prefs.Language ?? "en").ToLowerInvariant();
                    }
                }

                var customerName = item.CustomerName ?? "customer";
                var companyName  = item.CompanyName  ?? "MaidsFlow";

                // ----- SMS path -----
                if (item.Channel == "Sms")
                {
                    if (receiveSms == false || string.IsNullOrWhiteSpace(item.RecipientPhone))
                    {
                        skipped++;
                        continue;
                    }

                    var localTime = item.OccurrenceStartUtc.ToString("MMM dd, h:mm tt");
                    var body = lang switch
                    {
                        "pt-br" or "pt" => $"Oi {customerName}! Lembrete: seu serviço com {companyName} está agendado para {localTime}. Responda STOP para cancelar.",
                        "es" or "es-es" => $"Hola {customerName}! Recordatorio: tu servicio con {companyName} está programado para {localTime}. Responde STOP para cancelar.",
                        "fr" or "fr-fr" => $"Bonjour {customerName}! Rappel: votre service avec {companyName} est prévu pour {localTime}. Répondez STOP pour annuler.",
                        _ => $"Hi {customerName}! Reminder: your appointment with {companyName} is scheduled for {localTime}. Reply STOP to opt out.",
                    };

                    var r = await _smsDispatch.DispatchAsync(new SmsDispatchRequest
                    {
                        CompanyId = item.CompanyId,
                        AppointmentId = item.AppointmentId,
                        SeriesId = item.SeriesId,
                        OccurrenceStartUtc = item.OccurrenceStartUtc,
                        OccurrenceEndUtc = item.OccurrenceEndUtc,
                        Kind = AppointmentMessageKind.ConfirmationSms24h,
                        ToPhoneE164 = item.RecipientPhone,
                        Body = body,
                        TemplateKey = "sms.appointmentReminder24h.adminTriggered",
                    }, ct);

                    if (r.Outcome == SmsDispatchOutcome.Sent) sent++;
                    else { failed++; errors.Add($"SMS appt#{item.AppointmentId}: {r.Outcome}"); }
                    continue;
                }

                // ----- Email path -----
                if (item.Channel == "Email")
                {
                    if (receiveEmail == false || string.IsNullOrWhiteSpace(item.RecipientEmail))
                    {
                        skipped++;
                        continue;
                    }

                    // Defensive re-check: another worker / second click may have queued the same email.
                    var alreadyForOcc = await _db.AppointmentMessageLogs
                        .AnyAsync(l => l.AppointmentId == item.AppointmentId &&
                                       l.Kind == AppointmentMessageKind.ReminderEmail48h &&
                                       l.Channel == AppointmentMessageChannel.Email &&
                                       (l.Status == AppointmentMessageStatus.Sent ||
                                        l.Status == AppointmentMessageStatus.Pending) &&
                                       (l.OccurrenceStartUtc == null ||
                                        l.OccurrenceStartUtc == item.OccurrenceStartUtc), ct);
                    if (alreadyForOcc) { skipped++; continue; }

                    var localTime = item.OccurrenceStartUtc.ToString("dddd, MMMM d, yyyy 'at' h:mm tt");
                    string subject; string html;
                    switch (lang)
                    {
                        case "pt-br":
                        case "pt":
                            subject = $"Lembrete: seu serviço com {companyName} é em 48 horas";
                            html = $"<p>Olá {customerName},</p><p>Este é um lembrete de que seu serviço com <strong>{companyName}</strong> está agendado para <strong>{localTime}</strong>.</p><p>Se precisar reagendar, entre em contato diretamente.</p><p>Obrigado!</p>";
                            break;
                        case "es":
                        case "es-es":
                            subject = $"Recordatorio: tu servicio con {companyName} es en 48 horas";
                            html = $"<p>Hola {customerName},</p><p>Este es un recordatorio de que tu servicio con <strong>{companyName}</strong> está programado para <strong>{localTime}</strong>.</p><p>Si necesitas reprogramar, comunícate con {companyName}.</p><p>¡Gracias!</p>";
                            break;
                        case "fr":
                        case "fr-fr":
                            subject = $"Rappel : votre service avec {companyName} dans 48 heures";
                            html = $"<p>Bonjour {customerName},</p><p>Ceci est un rappel pour votre service avec <strong>{companyName}</strong> prévu le <strong>{localTime}</strong>.</p><p>Si vous devez reprogrammer, contactez {companyName} directement.</p><p>Merci !</p>";
                            break;
                        default:
                            subject = $"Reminder: your appointment with {companyName} is in 48h";
                            html = $"<p>Hi {customerName},</p><p>This is a reminder that your appointment with <strong>{companyName}</strong> is scheduled for <strong>{localTime}</strong>.</p><p>Thank you!</p>";
                            break;
                    }

                    // Persist a Pending log first, then send.
                    var log = new AppointmentMessageLog
                    {
                        AppointmentId = item.AppointmentId,
                        SeriesId = item.SeriesId,
                        OccurrenceStartUtc = item.OccurrenceStartUtc,
                        OccurrenceEndUtc = item.OccurrenceEndUtc,
                        Kind = AppointmentMessageKind.ReminderEmail48h,
                        Channel = AppointmentMessageChannel.Email,
                        Status = AppointmentMessageStatus.Pending,
                        ScheduledForUtc = item.ScheduledForUtc,
                        Attempt = 1,
                        RecipientEmail = item.RecipientEmail,
                        Subject = subject,
                        BodyText = html,
                        TemplateKey = "email.appointmentReminder48h.adminTriggered",
                        Provider = "SendGrid",
                    };
                    _db.AppointmentMessageLogs.Add(log);
                    await _db.SaveChangesAsync(ct);

                    var res = await _emailSender.SendAsync(new SendGridEmailMessage(
                        ToEmail: item.RecipientEmail,
                        Subject: subject,
                        PlainText: System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty),
                        Html: html,
                        ToName: customerName
                    ), ct);

                    if (res.Ok)
                    {
                        log.Status = AppointmentMessageStatus.Sent;
                        log.SentAtUtc = DateTime.UtcNow;
                        log.ProviderStatus = "queued";
                        await _db.SaveChangesAsync(ct);
                        sent++;
                    }
                    else
                    {
                        log.Status = AppointmentMessageStatus.Failed;
                        log.LastError = res.Error;
                        log.LastErrorRaw = res.ResponseBody;
                        await _db.SaveChangesAsync(ct);
                        failed++;
                        errors.Add($"Email appt#{item.AppointmentId}: {res.Error}");
                    }
                    continue;
                }

                // Unknown channel
                skipped++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MessagingTodayService dispatch error appt {Id}", item.AppointmentId);
                failed++;
                errors.Add($"appt#{item.AppointmentId}: {ex.Message}");
            }
        }

        return new TodayRunResultDto
        {
            Sent = sent,
            Failed = failed,
            Skipped = skipped,
            Total = pending.Count,
            Errors = errors.Take(20).ToList(),
        };
    }

    // ----------------------------------------------------------------------
    // Occurrence collection — covers normals + recurring
    // ----------------------------------------------------------------------

    private record TargetOccurrence(int AppointmentId, int CompanyId, int? CustomerId, Guid? SeriesId, DateTime Start, DateTime End);

    private async Task<List<TargetOccurrence>> CollectOccurrencesAsync(DateTime windowStart, DateTime windowEnd, CancellationToken ct)
    {
        var list = new List<TargetOccurrence>();

        var normals = await _db.Appointments.AsNoTracking()
            .Where(a => !a.IsRecurring &&
                        a.Status == 0 &&
                        a.Start >= windowStart && a.Start <= windowEnd)
            .Select(a => new { a.Id, a.CompanyId, a.CustomerId, a.Start, a.End })
            .ToListAsync(ct);
        list.AddRange(normals.Select(n => new TargetOccurrence(n.Id, n.CompanyId, n.CustomerId, null, n.Start, n.End)));

        var anchors = await _db.Appointments.AsNoTracking()
            .Where(a => a.IsRecurring && a.Status == 0 &&
                        a.SeriesId != null && !string.IsNullOrWhiteSpace(a.RecurrenceRule) &&
                        a.Start <= windowEnd &&
                        (a.RecurrenceEnd == null || a.RecurrenceEnd >= windowStart))
            .ToListAsync(ct);

        var seriesIds = anchors.Select(a => a.SeriesId!.Value).Distinct().ToList();
        var exceptions = await _db.Set<AppointmentRecurrenceException>().AsNoTracking()
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
