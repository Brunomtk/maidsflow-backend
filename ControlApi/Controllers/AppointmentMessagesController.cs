using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
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

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromRoute] int appointmentId,
        [FromQuery] DateTime? occurrenceStartUtc,
        [FromQuery] DateTime? occurrenceEndUtc,
        CancellationToken ct)
    {
        var logs = await _svc.GetLogsAsync(appointmentId, occurrenceStartUtc, occurrenceEndUtc, ct);
        return Ok(logs);
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
        return Ok(created);
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
        return Ok(updated);
    }

    /// <summary>
    /// Reenvia um SMS baseado em um log existente (cria uma nova tentativa no histórico).
    /// </summary>
    [HttpPost("{logId:int}/resend-sms")]
    public async Task<IActionResult> ResendSms([FromRoute] int appointmentId, [FromRoute] int logId, CancellationToken ct)
    {
        var log = await _svc.ResendSmsAsync(appointmentId, logId, ct);
        return Ok(log);
    }

    /// <summary>
    /// Reenvia um Email baseado em um log existente (cria uma nova tentativa no histórico).
    /// </summary>
    [HttpPost("{logId:int}/resend-email")]
    public async Task<IActionResult> ResendEmail([FromRoute] int appointmentId, [FromRoute] int logId, CancellationToken ct)
    {
        var log = await _svc.ResendEmailAsync(appointmentId, logId, ct);
        return Ok(log);
    }
}
