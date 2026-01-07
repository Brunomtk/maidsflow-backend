using Core.DTO.Appointment;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using Services.Integrations.Twilio;
using Services.Security;
using System.Globalization;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AppointmentController : ControllerBase
    {
        private readonly IAppointmentService _appointmentService;
        private readonly DbContextClass _db;
        private readonly IScopeGuard _scope;
        private readonly ITwilioSmsSender _sms;

        public AppointmentController(
            IAppointmentService appointmentService,
            DbContextClass db,
            IScopeGuard scope,
            ITwilioSmsSender sms)
        {
            _appointmentService = appointmentService;
            _db = db;
            _scope = scope;
            _sms = sms;
        }

        /// <summary>
        /// Lista agendamentos com paginação e filtros diversos (companyId, customerId, status, etc).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPaged([FromQuery] AppointmentFiltersDTO filters)
        {
            var result = await _appointmentService.GetPagedAppointments(filters);
            return Ok(result);
        }

        /// <summary>
        /// Retorna um agendamento por ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var appointment = await _appointmentService.GetById(id);
            if (appointment == null)
                return NotFound();

            return Ok(appointment);
        }

        /// <summary>
        /// Cria um novo agendamento.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAppointmentDTO dto)
        {
            var success = await _appointmentService.Create(dto);
            if (!success)
                return BadRequest("Erro ao criar agendamento.");

            return Ok("Agendamento criado com sucesso.");
        }

        /// <summary>
        /// Atualiza um agendamento existente.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateAppointmentDTO dto)
        {
            var success = await _appointmentService.Update(id, dto);
            if (!success)
                return NotFound("Agendamento não encontrado ou erro ao atualizar.");

            return Ok("Agendamento atualizado com sucesso.");
        }

        /// <summary>
        /// Exclui um agendamento.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _appointmentService.Delete(id);
            if (!success)
                return NotFound("Agendamento não encontrado ou erro ao excluir.");

            return NoContent();
        }

        /// <summary>
        /// Envia SMS "On my way" via Twilio para o telefone do Customer do agendamento.
        /// Company/Professional só podem enviar se tiverem acesso ao appointment (ScopeGuard).
        /// </summary>
        [HttpPost("{id}/on-my-way-sms")]
        public async Task<IActionResult> SendOnMyWaySms(
            int id,
            [FromQuery] int? etaMinutes,
            [FromBody] OnMyWaySmsRequestDTO? request,
            CancellationToken ct)
        {
            // Segurança multi-tenant
            await _scope.EnsureAppointmentAccessAsync(id);

            var appt = await _db.Appointments
                .Include(a => a.Company)
                .Include(a => a.Customer)
                .FirstOrDefaultAsync(a => a.Id == id, ct);

            if (appt == null)
                return NotFound("Agendamento não encontrado.");

            var companyName = appt.Company?.Name ?? "Our Team";
            var customerName = appt.Customer?.Name ?? "there";
            var to = appt.Customer?.Phone ?? string.Empty;

            if (string.IsNullOrWhiteSpace(to))
                return BadRequest("Customer não possui telefone para envio de SMS.");

            var address = !string.IsNullOrWhiteSpace(appt.Address)
                ? appt.Address
                : (appt.Customer?.Address ?? string.Empty);

            var eta = request?.EtaMinutes ?? etaMinutes ?? 15;

            if (eta < 1 || eta > 240)
                return BadRequest("etaMinutes deve estar entre 1 e 240 minutos.");

            if (string.IsNullOrWhiteSpace(address))
                address = appt.Customer?.Address ?? string.Empty;

            if (string.IsNullOrWhiteSpace(address))
                return BadRequest("Não foi possível determinar o endereço do agendamento.");

            var body =
                $"Hi {customerName}, this is {companyName}. Reminder: our team is on the way and will arrive at your location in approximately {eta} minutes at {address}. Reply HELP for help or STOP to unsubscribe.";

var (sid, raw) = await _sms.SendSmsAsync(to, body, ct);

            return Ok(new
            {
                appointmentId = id,
                to,
                messageSid = sid,
                body
            });
        }
    }
}
