using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using Core.Models;
using Core.DTOs.Messaging;

namespace ControlApi.Controllers;

[ApiController]
[Route("api/appointments/{appointmentId:int}/messages")]
[Authorize]
public class AppointmentMessagesController : ControllerBase
{
    private readonly IAppointmentMessageLogService _svc;

    public AppointmentMessagesController(IAppointmentMessageLogService svc)
    {
        _svc = svc;
    }


private static AppointmentMessageLogDto ToDto(AppointmentMessageLog l)
{
    // Backend enums are 1-based; frontend expects 0-based.
    var channel = Math.Max(0, ((int)l.Channel) - 1);
    var status = Math.Max(0, ((int)l.Status) - 1);

    return new AppointmentMessageLogDto
    {
        Id = l.Id,
        AppointmentId = l.AppointmentId,
        Kind = (int)l.Kind,
        Channel = channel,
        Status = status,
        ScheduledForUtc = l.ScheduledForUtc,
        SentAtUtc = l.SentAtUtc,
        Attempt = l.Attempt,
        RequestedByUserId = l.RequestedByUserId,
        RequestedByRole = l.RequestedByRole,
        RecipientEmail = l.RecipientEmail,
        RecipientPhoneE164 = l.RecipientPhoneE164,
        Subject = l.Subject,
        BodyText = l.BodyText,
        TemplateKey = l.TemplateKey,
        PayloadJson = l.PayloadJson,
        Provider = l.Provider,
        ProviderMessageId = l.ProviderMessageId,
        ProviderStatus = l.ProviderStatus,
        LastError = l.LastError,
        LastErrorRaw = l.LastErrorRaw,
        CreatedDate = l.CreatedDate,
        UpdatedDate = l.UpdatedDate,
        SeriesId = l.SeriesId,
        OccurrenceStartUtc = l.OccurrenceStartUtc,
        OccurrenceEndUtc = l.OccurrenceEndUtc
    };
}

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromRoute] int appointmentId,
        [FromQuery] DateTime? occurrenceStartUtc,
        [FromQuery] DateTime? occurrenceEndUtc,
        CancellationToken ct)
    {
        var logs = await _svc.GetLogsAsync(appointmentId, occurrenceStartUtc, occurrenceEndUtc, ct);
        return Ok(logs.Select(ToDto).ToList());
}

    /// <summary>
    /// Cria um log de mensagem para um appointment (usado pelo n8n antes de enviar SMS/Email).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateLog(
        [FromRoute] int appointmentId,
        [FromBody] CreateAppointmentMessageLogRequest req,
        CancellationToken ct)
    {
        var created = await _svc.CreateLogAsync(appointmentId, req, ct);
        return Ok(ToDto(created));
}

    /// <summary>
    /// Atualiza um log existente (usado pelo n8n após o envio para marcar Sent/Failed).
    /// </summary>
    [HttpPatch("{logId:int}")]
    public async Task<IActionResult> UpdateLog(
        [FromRoute] int appointmentId,
        [FromRoute] int logId,
        [FromBody] UpdateAppointmentMessageLogRequest req,
        CancellationToken ct)
    {
        var updated = await _svc.UpdateLogAsync(appointmentId, logId, req, ct);
        return Ok(ToDto(updated));
}

    /// <summary>
    /// Reenvia um SMS baseado em um log existente (cria uma nova tentativa no histórico).
    /// </summary>
    [HttpPost("{logId:int}/resend-sms")]
    public async Task<IActionResult> ResendSms([FromRoute] int appointmentId, [FromRoute] int logId, CancellationToken ct)
    {
        var log = await _svc.ResendSmsAsync(appointmentId, logId, ct);
        return Ok(ToDto(log));
}

    /// <summary>
    /// Reenvia um Email baseado em um log existente (cria uma nova tentativa no histórico).
    /// </summary>
    [HttpPost("{logId:int}/resend-email")]
    public async Task<IActionResult> ResendEmail([FromRoute] int appointmentId, [FromRoute] int logId, CancellationToken ct)
    {
        var log = await _svc.ResendEmailAsync(appointmentId, logId, ct);
        return Ok(ToDto(log));
}
}
