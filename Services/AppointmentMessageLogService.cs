using Core.Enums.Messaging;
using Core.DTOs.Messaging;
using Core.Exceptions;
using Core.Models;
using Infrastructure;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Services.Integrations.SendGrid;
using Services.Integrations.Twilio;
using Services.Security;
using System.Linq;

namespace Services;

public interface IAppointmentMessageLogService
{
    Task<IReadOnlyList<AppointmentMessageLog>> GetLogsAsync(int appointmentId, DateTime? occurrenceStartUtc = null, DateTime? occurrenceEndUtc = null, CancellationToken ct = default);
    Task EnsureDefaultPlaceholdersAsync(Appointment appointment, DateTime? occurrenceStartUtc = null, DateTime? occurrenceEndUtc = null, CancellationToken ct = default);
    Task<AppointmentMessageLog> CreateLogAsync(int appointmentId, CreateAppointmentMessageLogRequest req, CancellationToken ct = default);
    Task<AppointmentMessageLog> UpdateLogAsync(int appointmentId, int logId, UpdateAppointmentMessageLogRequest req, CancellationToken ct = default);
    Task<AppointmentMessageLog> ResendSmsAsync(int appointmentId, int logId, CancellationToken ct = default);
    Task<AppointmentMessageLog> ResendEmailAsync(int appointmentId, int logId, CancellationToken ct = default);
}

public class AppointmentMessageLogService : IAppointmentMessageLogService
{
    private readonly IUnitOfWork _uow;
    private readonly DbContextClass _db;
    private readonly ICurrentUser _currentUser;
    private readonly IScopeGuard _scope;
    private readonly ITwilioSmsSender _twilio;
    private readonly ISendGridEmailSender _sendGrid;

    public AppointmentMessageLogService(
        IUnitOfWork uow,
        DbContextClass db,
        ICurrentUser currentUser,
        IScopeGuard scope,
        ITwilioSmsSender twilio,
        ISendGridEmailSender sendGrid)
    {
        _uow = uow;
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
        _twilio = twilio;
        _sendGrid = sendGrid;
    }

    public async Task<IReadOnlyList<AppointmentMessageLog>> GetLogsAsync(int appointmentId, DateTime? occurrenceStartUtc = null, DateTime? occurrenceEndUtc = null, CancellationToken ct = default)
    {
        var appt = await _uow.Appointments.GetById(appointmentId);
        if (appt == null) throw new NotFoundException("Agendamento não encontrado.");

        await EnsureAppointmentAccessAsync(appt);

        // Garantir placeholders (48h Email / 24h SMS) para aparecer na UI mesmo antes do primeiro envio.
        // Normalize occurrence timestamps to reduce duplication caused by tick/second differences.
        var normStart = NormalizeOccurrenceUtc(occurrenceStartUtc);
        var normEnd = NormalizeOccurrenceUtc(occurrenceEndUtc);

        await EnsureDefaultPlaceholdersAsync(appt, normStart, normEnd, ct);

        // For recurring appointments, the UI can request logs for a specific occurrence by passing occurrenceStartUtc.
        // Match by occurrence window when provided (recurrence-safe)
        var logs = await _uow.AppointmentMessageLogs.GetByAppointmentAsync(appointmentId, normStart, normEnd, ct);

        // SAFETY: Only show "Sent" when we have a real SentAtUtc.
        // This avoids incorrect UI (Status=Sent with empty sent timestamp).
        foreach (var l in logs)
        {
            if (l.Status == AppointmentMessageStatus.Sent && l.SentAtUtc == null)
            {
                l.Status = AppointmentMessageStatus.Pending;
            }
        }

        return logs;
    }

    public async Task EnsureDefaultPlaceholdersAsync(Appointment appointment, DateTime? occurrenceStartUtc = null, DateTime? occurrenceEndUtc = null, CancellationToken ct = default)
    {
        var isRecurringContext = appointment.IsRecurring || appointment.SeriesId.HasValue;
        var occStartUtc = NormalizeOccurrenceUtc(occurrenceStartUtc);
        var occEndUtc = NormalizeOccurrenceUtc(occurrenceEndUtc);

        if (isRecurringContext)
        {
            // If caller didn't pass occurrence dates, fall back to the appointment's own Start/End.
            // (Still better than nothing, but ideally the UI sends the occurrence Start/End.)
            if (!occStartUtc.HasValue) occStartUtc = NormalizeOccurrenceUtc(EnsureUtc(appointment.Start));
            if (!occEndUtc.HasValue) occEndUtc = NormalizeOccurrenceUtc(EnsureUtc(appointment.End));
        }

        // Cria placeholders apenas 1x por kind/channel.
        // Não bloqueia se já existe qualquer log (inclusive de reenvio).
        var existing48 = await _uow.AppointmentMessageLogs.GetLatestAsync(
            appointment.Id, AppointmentMessageKind.ReminderEmail48h, AppointmentMessageChannel.Email, occStartUtc, occEndUtc, ct);
        var existing24 = await _uow.AppointmentMessageLogs.GetLatestAsync(
            appointment.Id, AppointmentMessageKind.ConfirmationSms24h, AppointmentMessageChannel.Sms, occStartUtc, occEndUtc, ct);

        var now = DateTime.UtcNow;
        var startUtc = occStartUtc ?? NormalizeOccurrenceUtc(EnsureUtc(appointment.Start)) ?? EnsureUtc(appointment.Start);

        
        // Carregar dados necessários para compor mensagens padrão (nome/contato da Company, nome/telefone do Customer, endereço).
        var apptFull = await _db.Appointments
            .Include(a => a.Company)
            .Include(a => a.Customer)
            .Include(a => a.CustomerAddress)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == appointment.Id, ct);

        var companyName = apptFull?.Company?.Name ?? "Our Team";
        var companyPhone = apptFull?.Company?.Phone ?? "";
        var companyEmail = apptFull?.Company?.Email ?? "";
        var customerName = apptFull?.Customer?.Name ?? "there";
        var customerPhone = apptFull?.Customer?.Phone ?? "";
        var customerEmail = apptFull?.Customer?.Email ?? "";

        var address = BuildBestAddress(apptFull ?? appointment);
        // Prefer occurrence start label when provided.
        var startForLabel = startUtc;
        var startLabel = startForLabel.ToString("MMM dd, yyyy 'at' HH:mm");

        var createdAny = false;

        if (existing48 == null)
        {
            await _uow.AppointmentMessageLogs.Add(new AppointmentMessageLog
            {
                AppointmentId = appointment.Id,
                SeriesId = isRecurringContext ? appointment.SeriesId : null,
                OccurrenceStartUtc = isRecurringContext ? startUtc : null,
                OccurrenceEndUtc = isRecurringContext ? (occEndUtc ?? EnsureUtc(appointment.End)) : null,
                Kind = AppointmentMessageKind.ReminderEmail48h,
                Channel = AppointmentMessageChannel.Email,
                Status = AppointmentMessageStatus.Pending,
                ScheduledForUtc = startUtc.AddHours(-48),
                Attempt = 0,
                Provider = "SendGrid",
                RequestedByRole = "System",
                CreatedDate = now,
                RecipientEmail = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail,
                Subject = $"DON'T REPLY — Appointment reminder ({startLabel})",
                // Stored in BodyText (column is text) because the migration doesn't have a separate preview column.
                BodyText = $"DON'T REPLY. If you need to change your appointment, get in touch with ELIZA at {companyPhone} or {companyEmail}.",
                TemplateKey = "appointment_reminder_48h_v1",
                UpdatedDate = now
            });
            createdAny = true;
        }

        if (existing24 == null)
        {
            await _uow.AppointmentMessageLogs.Add(new AppointmentMessageLog
            {
                AppointmentId = appointment.Id,
                SeriesId = isRecurringContext ? appointment.SeriesId : null,
                OccurrenceStartUtc = isRecurringContext ? startUtc : null,
                OccurrenceEndUtc = isRecurringContext ? (occEndUtc ?? EnsureUtc(appointment.End)) : null,
                Kind = AppointmentMessageKind.ConfirmationSms24h,
                Channel = AppointmentMessageChannel.Sms,
                Status = AppointmentMessageStatus.Pending,
                ScheduledForUtc = startUtc.AddHours(-24),
                Attempt = 0,
                Provider = "Twilio",
                RequestedByRole = "System",
                CreatedDate = now,
                RecipientPhoneE164 = customerPhone,
                BodyText = BuildConfirmationSms24h(customerName, companyName, companyPhone, companyEmail, address, startLabel),
                TemplateKey = "appointment_confirmation_24h_sms_v1",
                UpdatedDate = now
            });
            createdAny = true;
        }

        if (createdAny)
            await _uow.SaveAsync();
    }

    public async Task<AppointmentMessageLog> CreateLogAsync(int appointmentId, CreateAppointmentMessageLogRequest req, CancellationToken ct = default)
    {
        var appt = await _uow.Appointments.GetById(appointmentId);
        if (appt == null) throw new NotFoundException("Agendamento não encontrado.");

        await EnsureAppointmentAccessAsync(appt);

        var kind = ParseEnum<AppointmentMessageKind>(req.Kind, nameof(req.Kind));
        var channel = ParseEnum<AppointmentMessageChannel>(req.Channel, nameof(req.Channel));
        var status = ParseEnum<AppointmentMessageStatus>(req.Status, nameof(req.Status));

        var occStart = NormalizeOccurrenceUtc(req.OccurrenceStartUtc);
        var occEnd = NormalizeOccurrenceUtc(req.OccurrenceEndUtc);

        // Attempt per occurrence/kind/channel
        var attempt = await _uow.AppointmentMessageLogs.GetNextAttemptAsync(appointmentId, kind, channel, occStart, occEnd, ct);

        var provider = channel == AppointmentMessageChannel.Sms ? "Twilio" : "SendGrid";

        // Create should normally start Pending. If caller tries to create as Sent, we downgrade to Pending.
        if (status == AppointmentMessageStatus.Sent)
        {
            status = AppointmentMessageStatus.Pending;
        }

        var log = new AppointmentMessageLog
        {
            AppointmentId = appointmentId,
            SeriesId = appt.SeriesId,
            OccurrenceStartUtc = occStart,
            OccurrenceEndUtc = occEnd,
            Kind = kind,
            Channel = channel,
            Status = status,
            ScheduledForUtc = req.ScheduledForUtc,
            SentAtUtc = null,
            Attempt = attempt,
            RequestedByUserId = _currentUser.UserId,
            RequestedByRole = string.IsNullOrWhiteSpace(req.RequestedByRole) ? "System" : req.RequestedByRole,
            RecipientEmail = req.RecipientEmail,
            RecipientPhoneE164 = req.RecipientPhoneE164,
            Subject = req.Subject,
            BodyText = req.BodyText,
            TemplateKey = req.TemplateKey,
            PayloadJson = req.PayloadJson,
            Provider = provider,
            ProviderMessageId = null,
            ProviderStatus = null,
            LastError = null,
            LastErrorRaw = null,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        await _db.AppointmentMessageLogs.AddAsync(log, ct);
        await _uow.SaveAsync();
        return log;
    }

    public async Task<AppointmentMessageLog> UpdateLogAsync(int appointmentId, int logId, UpdateAppointmentMessageLogRequest req, CancellationToken ct = default)
    {
        var appt = await _uow.Appointments.GetById(appointmentId);
        if (appt == null) throw new NotFoundException("Agendamento não encontrado.");

        await EnsureAppointmentAccessAsync(appt);

        var log = await _db.AppointmentMessageLogs.FirstOrDefaultAsync(x => x.Id == logId && x.AppointmentId == appointmentId, ct);
        if (log == null) throw new NotFoundException("Log de mensagem não encontrado.");

        if (!string.IsNullOrWhiteSpace(req.Status))
        {
            var newStatus = ParseEnum<AppointmentMessageStatus>(req.Status!, nameof(req.Status));

            // Only allow Sent if SentAtUtc provided
            if (newStatus == AppointmentMessageStatus.Sent && req.SentAtUtc == null)
            {
                newStatus = AppointmentMessageStatus.Pending;
            }

            log.Status = newStatus;
        }

        if (req.SentAtUtc.HasValue)
            log.SentAtUtc = EnsureUtc(req.SentAtUtc.Value);

        if (!string.IsNullOrWhiteSpace(req.ProviderMessageId))
            log.ProviderMessageId = req.ProviderMessageId;

        if (!string.IsNullOrWhiteSpace(req.ProviderStatus))
            log.ProviderStatus = req.ProviderStatus;

        if (!string.IsNullOrWhiteSpace(req.LastError))
            log.LastError = req.LastError;

        if (!string.IsNullOrWhiteSpace(req.LastErrorRaw))
            log.LastErrorRaw = req.LastErrorRaw;

        // FINAL SAFETY: Sent must have SentAtUtc
        if (log.Status == AppointmentMessageStatus.Sent && log.SentAtUtc == null)
            log.Status = AppointmentMessageStatus.Pending;

        log.UpdatedDate = DateTime.UtcNow;
        await _uow.SaveAsync();
        return log;
    }

    private static DateTime EnsureUtc(DateTime dt)
        => dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    private static DateTime? NormalizeOccurrenceUtc(DateTime? dt)
    {
        if (!dt.HasValue) return null;
        var utc = EnsureUtc(dt.Value);
        // Normalize to minute precision (seconds and ticks removed) to avoid mismatch between sources.
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, DateTimeKind.Utc);
    }

    private static TEnum ParseEnum<TEnum>(string? raw, string fieldName) where TEnum : struct
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new BadRequestException($"Campo '{fieldName}' é obrigatório.");

        var s = raw.Trim();

        // Accept numeric strings ("1")
        if (int.TryParse(s, out var num))
        {
            if (Enum.IsDefined(typeof(TEnum), num))
                return (TEnum)Enum.ToObject(typeof(TEnum), num);
        }

        // Accept names (case-insensitive)
        if (Enum.TryParse<TEnum>(s, ignoreCase: true, out var parsed))
            return parsed;

        throw new BadRequestException($"Valor inválido para '{fieldName}': '{raw}'.");
    }

    public async Task<AppointmentMessageLog> ResendSmsAsync(int appointmentId, int logId, CancellationToken ct = default)
    {
        var appt = await _uow.Appointments.GetById(appointmentId);
        if (appt == null) throw new NotFoundException("Agendamento não encontrado.");
        await EnsureAppointmentAccessAsync(appt);

        var existing = await _db.AppointmentMessageLogs.FirstOrDefaultAsync(x => x.Id == logId && x.AppointmentId == appointmentId, ct);
        if (existing == null) throw new NotFoundException("Log de mensagem não encontrado.");

        if (existing.Channel != AppointmentMessageChannel.Sms)
            throw new BadRequestException("Reenvio disponível apenas para SMS no momento.");

        var to = existing.RecipientPhoneE164;
        if (string.IsNullOrWhiteSpace(to))
        {
            // tentar pegar do Customer
            if (!appt.CustomerId.HasValue)
                throw new BadRequestException("Agendamento não possui cliente associado.");

            var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == appt.CustomerId.Value, ct);
            to = customer?.Phone;
        }

        if (string.IsNullOrWhiteSpace(to))
            throw new BadRequestException("Telefone do cliente não encontrado para reenviar SMS.");

        var body = existing.BodyText;
        if (string.IsNullOrWhiteSpace(body))
            throw new BadRequestException("Conteúdo do SMS não está disponível neste log para reenviar.");

        var attempt = await _uow.AppointmentMessageLogs.GetNextAttemptAsync(appointmentId, existing.Kind, existing.Channel, existing.OccurrenceStartUtc, existing.OccurrenceEndUtc, ct);
        var now = DateTime.UtcNow;

        var newLog = new AppointmentMessageLog
        {
            AppointmentId = appointmentId,
            SeriesId = existing.SeriesId,
            OccurrenceStartUtc = existing.OccurrenceStartUtc,
            OccurrenceEndUtc = existing.OccurrenceEndUtc,
            Kind = existing.Kind,
            Channel = AppointmentMessageChannel.Sms,
            Status = AppointmentMessageStatus.Pending,
            ScheduledForUtc = existing.ScheduledForUtc,
            Attempt = attempt,
            Provider = "Twilio",
            RequestedByUserId = _currentUser.UserId,
            RequestedByRole = _currentUser.IsAdmin ? "Admin" : (_currentUser.IsProfessional ? "Professional" : "Company"),
            RecipientPhoneE164 = to,
            BodyText = body,
            TemplateKey = existing.TemplateKey,
            PayloadJson = existing.PayloadJson,
            CreatedDate = now,
            UpdatedDate = now
        };

        await _uow.AppointmentMessageLogs.Add(newLog);
        await _uow.SaveAsync();

        try
        {
            var (sid, raw) = await _twilio.SendSmsAsync(to!, body!, ct);

            // Only mark as Sent when we have a concrete send marker.
            // For Twilio, the SID is the most reliable indicator.
            if (string.IsNullOrWhiteSpace(sid))
                throw new Exception("Twilio did not return a message SID.");

            newLog.Status = AppointmentMessageStatus.Sent;
            newLog.SentAtUtc = DateTime.UtcNow;
            newLog.ProviderMessageId = sid;
            newLog.ProviderStatus = "accepted";
            newLog.LastError = null;
            newLog.LastErrorRaw = null;
            newLog.UpdatedDate = DateTime.UtcNow;

            _uow.AppointmentMessageLogs.Update(newLog);
            await _uow.SaveAsync();
        }
        catch (Exception ex)
        {
            newLog.Status = AppointmentMessageStatus.Failed;
            newLog.LastError = ex.Message;
            newLog.UpdatedDate = DateTime.UtcNow;
            _uow.AppointmentMessageLogs.Update(newLog);
            await _uow.SaveAsync();

            // Repropaga para UI saber que falhou (mas já está logado)
            throw;
        }

        return newLog;
    }

    public async Task<AppointmentMessageLog> ResendEmailAsync(int appointmentId, int logId, CancellationToken ct = default)
    {
        var appt = await _uow.Appointments.GetById(appointmentId);
        if (appt == null) throw new NotFoundException("Agendamento não encontrado.");
        await EnsureAppointmentAccessAsync(appt);

        var existing = await _db.AppointmentMessageLogs.FirstOrDefaultAsync(x => x.Id == logId && x.AppointmentId == appointmentId, ct);
        if (existing == null) throw new NotFoundException("Log de mensagem não encontrado.");

        if (existing.Channel != AppointmentMessageChannel.Email)
            throw new BadRequestException("Reenvio disponível apenas para Email neste endpoint.");

        // Resolve destinatário
        var toEmail = existing.RecipientEmail;
        string? toName = null;

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            if (!appt.CustomerId.HasValue)
                throw new BadRequestException("Agendamento não possui cliente associado.");

            var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == appt.CustomerId.Value, ct);
            toEmail = customer?.Email;
            toName = customer?.Name;
        }

        if (string.IsNullOrWhiteSpace(toEmail))
            throw new BadRequestException("Email do cliente não encontrado para reenviar.");

        // Dados da company para copy padrão
        var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == appt.CompanyId, ct);
        var companyName = company?.Name ?? "Our Team";
        var companyPhone = company?.Phone ?? "";
        var companyEmail = company?.Email ?? "";

        var subject = string.IsNullOrWhiteSpace(existing.Subject)
            ? "DON'T REPLY — Appointment confirmation"
            : existing.Subject;

        var plain = string.IsNullOrWhiteSpace(existing.BodyText)
            ? $"DON'T REPLY. If you need to change your appointment, get in touch with ELIZA at {companyPhone} or {companyEmail}."
            : existing.BodyText;

        var html = BuildEmailHtml(companyName, plain);

        var attempt = await _uow.AppointmentMessageLogs.GetNextAttemptAsync(appointmentId, existing.Kind, existing.Channel, existing.OccurrenceStartUtc, existing.OccurrenceEndUtc, ct);
        var now = DateTime.UtcNow;

        var newLog = new AppointmentMessageLog
        {
            AppointmentId = appointmentId,
            SeriesId = existing.SeriesId,
            OccurrenceStartUtc = existing.OccurrenceStartUtc,
            OccurrenceEndUtc = existing.OccurrenceEndUtc,
            Kind = existing.Kind,
            Channel = AppointmentMessageChannel.Email,
            Status = AppointmentMessageStatus.Pending,
            ScheduledForUtc = existing.ScheduledForUtc,
            Attempt = attempt,
            Provider = "SendGrid",
            RequestedByUserId = _currentUser.UserId,
            RequestedByRole = _currentUser.IsAdmin ? "Admin" : (_currentUser.IsProfessional ? "Professional" : "Company"),
            RecipientEmail = toEmail,
            Subject = subject,
            BodyText = plain,
            TemplateKey = existing.TemplateKey,
            PayloadJson = existing.PayloadJson,
            CreatedDate = now,
            UpdatedDate = now
        };

        await _uow.AppointmentMessageLogs.Add(newLog);
        await _uow.SaveAsync();

        var send = await _sendGrid.SendAsync(new SendGridEmailMessage(
            ToEmail: toEmail!,
            Subject: subject,
            PlainText: plain,
            Html: html,
            ToName: toName
        ), ct);

        if (send.Ok)
        {
            newLog.Status = AppointmentMessageStatus.Sent;
            newLog.SentAtUtc = DateTime.UtcNow;
            newLog.ProviderStatus = $"accepted:{send.StatusCode}";
            newLog.LastError = null;
            newLog.LastErrorRaw = null;
            newLog.UpdatedDate = DateTime.UtcNow;
            _uow.AppointmentMessageLogs.Update(newLog);
            await _uow.SaveAsync();
            return newLog;
        }

        newLog.Status = AppointmentMessageStatus.Failed;
        newLog.ProviderStatus = send.StatusCode == 0 ? null : send.StatusCode.ToString();
        newLog.LastError = send.Error ?? "SendGrid request failed";
        newLog.LastErrorRaw = send.ResponseBody;
        newLog.UpdatedDate = DateTime.UtcNow;
        _uow.AppointmentMessageLogs.Update(newLog);
        await _uow.SaveAsync();

        throw new BadRequestException($"Falha ao reenviar email: {newLog.LastError}");
    }

    private static string BuildEmailHtml(string companyName, string plainText)
    {
        var safe = System.Net.WebUtility.HtmlEncode(plainText).Replace("\n", "<br/>");
        return $@"<!doctype html>
<html>
  <head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1' />
  </head>
  <body style='margin:0;padding:0;background:#0b1220;font-family:Arial,Helvetica,sans-serif;'>
    <div style='max-width:640px;margin:0 auto;padding:24px;'>
      <div style='background:#0f1a33;border:1px solid rgba(255,255,255,0.12);border-radius:16px;overflow:hidden;'>
        <div style='padding:18px 20px;border-bottom:1px solid rgba(255,255,255,0.10);'>
          <div style='color:#7dd3fc;font-weight:700;font-size:14px;letter-spacing:0.08em;text-transform:uppercase;'>MaidsFlow</div>
          <div style='color:#ffffff;font-weight:700;font-size:18px;margin-top:6px;'>DON'T REPLY</div>
          <div style='color:rgba(255,255,255,0.65);font-size:13px;margin-top:6px;'>This email was sent by {System.Net.WebUtility.HtmlEncode(companyName)}.</div>
        </div>
        <div style='padding:18px 20px;color:#ffffff;font-size:14px;line-height:1.55;'>
          {safe}
        </div>
      </div>
      <div style='color:rgba(255,255,255,0.45);font-size:12px;margin-top:14px;text-align:center;'>
        Please do not reply to this email.
      </div>
    </div>
  </body>
</html>";
    }

    
private static string BuildConfirmationSms24h(
    string customerName,
    string companyName,
    string companyPhone,
    string companyEmail,
    string address,
    string startLabel)
{
    var contact = BuildContactLine(companyPhone, companyEmail);
    return $"DON'T REPLY. Hi {customerName}, this is {companyName}. " +
           $"Reminder: your appointment is scheduled for {startLabel}" +
           (string.IsNullOrWhiteSpace(address) ? "." : $" at {address}.") +
           $" If you need to change your appointment, get in touch with ELIZA{contact}.";
}

private static string BuildContactLine(string phone, string email)
{
    phone = (phone ?? string.Empty).Trim();
    email = (email ?? string.Empty).Trim();

    if (!string.IsNullOrWhiteSpace(phone) && !string.IsNullOrWhiteSpace(email))
        return $" at {phone} or {email}";
    if (!string.IsNullOrWhiteSpace(phone))
        return $" at {phone}";
    if (!string.IsNullOrWhiteSpace(email))
        return $" at {email}";

    return string.Empty;
}

private static string BuildBestAddress(Appointment appt)
{
    if (!string.IsNullOrWhiteSpace(appt.Address))
        return appt.Address;

    if (appt.CustomerAddress != null)
    {
        var parts = new[]
        {
            appt.CustomerAddress.AddressLine1,
            appt.CustomerAddress.City,
            appt.CustomerAddress.State,
            appt.CustomerAddress.ZipCode
        }.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

        var line = string.Join(", ", parts);
        if (!string.IsNullOrWhiteSpace(line))
            return line;
    }

    if (appt.Customer != null && !string.IsNullOrWhiteSpace(appt.Customer.Address))
        return appt.Customer.Address;

    return string.Empty;
}

private async Task EnsureAppointmentAccessAsync(Appointment appointment)
    {
        if (_currentUser.IsAdmin) return;

        await _scope.EnsureCompanyAccessAsync(appointment.CompanyId);

        if (_currentUser.IsProfessional)
        {
            var professionalId = await _scope.GetScopedProfessionalIdAsync();
            if (!professionalId.HasValue || !appointment.ProfessionalIds.Contains(professionalId.Value))
                throw new ForbiddenException("Você não tem permissão para acessar este agendamento.");
        }
    }
}
