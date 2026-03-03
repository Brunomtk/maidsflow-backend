using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

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
