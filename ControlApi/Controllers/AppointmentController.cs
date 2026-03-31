using Core.DTO.Appointment;
using Infrastructure;
using Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using Services.Integrations.Twilio;
using Services.Security;
using Services.Storage;
using System.Text.Json;
using Core.DTO.Customer;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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
        private readonly IS3StorageService _s3;

        public AppointmentController(
            IAppointmentService appointmentService,
            DbContextClass db,
            IScopeGuard scope,
            ITwilioSmsSender sms,
            IS3StorageService s3)
        {
            _appointmentService = appointmentService;
            _db = db;
            _scope = scope;
            _sms = sms;
            _s3 = s3;
        }

        private static string BuildCustomerAddressLine(CustomerAddress addr)
        {
            var parts = new List<string>();
            var line1 = addr.AddressLine1?.Trim();
            var line2 = addr.AddressLine2?.Trim();
            if (!string.IsNullOrWhiteSpace(line1)) parts.Add(line1!);
            if (!string.IsNullOrWhiteSpace(line2)) parts.Add(line2!);

            var cityState = string.Join(", ", new[] { addr.City?.Trim(), addr.State?.Trim() }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (!string.IsNullOrWhiteSpace(cityState)) parts.Add(cityState);

            var zip = addr.ZipCode?.Trim();
            if (!string.IsNullOrWhiteSpace(zip)) parts.Add(zip!);

            return string.Join(" - ", parts);
        }


        private HouseNotesResponseDTO? ResolveHouseNotesResponse(string? snapshotJson, int? customerAddressId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(snapshotJson))
                    return null;

                var snapshot = JsonSerializer.Deserialize<HouseNotesSnapshotDTO>(snapshotJson);
                if (snapshot == null)
                    return null;

                var photoKeys = (snapshot.PhotoUrls ?? new List<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => _s3.TryGetKeyFromStoredValue(x, out var key) ? key : x)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(System.StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new HouseNotesResponseDTO
                {
                    AddressId = snapshot.CustomerAddressId ?? customerAddressId ?? 0,
                    CustomerId = 0,
                    AccessNotes = snapshot.AccessNotes,
                    GateCode = snapshot.GateCode,
                    HasPets = snapshot.HasPets,
                    PetNotes = snapshot.PetNotes,
                    RestrictionsNotes = snapshot.RestrictionsNotes,
                    PriorityNotes = snapshot.PriorityNotes,
                    PhotoKeys = photoKeys,
                    PhotoUrls = photoKeys.Select(x => _s3.CreateDownloadUrl(x) ?? x).ToList()
                };
            }
            catch
            {
                return null;
            }
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

        [HttpGet("{id}/house-notes")]
        public async Task<IActionResult> GetHouseNotes(int id)
        {
            var appointment = await _appointmentService.GetById(id);
            if (appointment == null)
                return NotFound();

            return Ok(new
            {
                appointment.Id,
                appointment.CustomerId,
                appointment.CustomerAddressId,
                appointment.HouseNotesSnapshotJson,
                HouseNotes = ResolveHouseNotesResponse(appointment.HouseNotesSnapshotJson, appointment.CustomerAddressId)
            });
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
        /// Creates (or returns an existing) appointment linked to a Guesty reservation.
        /// Used by the Guesty calendar overlay to push reservations/blocks into the MaidsFlow calendar
        /// (button "Create Appointment" or drag-and-drop).
        /// </summary>
        [HttpPost("from-guesty")]
        public async Task<IActionResult> CreateFromGuesty([FromBody] CreateAppointmentFromGuestyDTO dto)
        {
            var appt = await _appointmentService.CreateFromGuestyAsync(dto);
            return Ok(appt);
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
                .Include(a => a.CustomerAddress)
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
                : (!string.IsNullOrWhiteSpace(appt.CustomerAddress?.AddressLine1)
                    ? BuildCustomerAddressLine(appt.CustomerAddress)
                    : (appt.Customer?.Address ?? string.Empty));

            var eta = request?.EtaMinutes ?? etaMinutes ?? 15;

            if (eta < 1 || eta > 240)
                return BadRequest("etaMinutes deve estar entre 1 e 240 minutos.");

            if (string.IsNullOrWhiteSpace(address))
                address = !string.IsNullOrWhiteSpace(appt.CustomerAddress?.AddressLine1)
                    ? BuildCustomerAddressLine(appt.CustomerAddress)
                    : (appt.Customer?.Address ?? string.Empty);

            if (string.IsNullOrWhiteSpace(address))
                return BadRequest("Não foi possível determinar o endereço do agendamento.");

            var body =
                $"DON'T REPLY. Hi {customerName}, this is {companyName}. Our team is on the way and will arrive in approximately {eta} minutes at {address}. " +
                $"If you need to change your appointment, get in touch with {companyName}. Reply STOP to unsubscribe.";
try
            {
                var (sid, _) = await _sms.SendSmsAsync(to, body, ct);

                return Ok(new
                {
                    appointmentId = id,
                    to,
                    messageSid = sid,
                    body
                });
            }
            catch (TwilioValidationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (TwilioConfigurationException ex)
            {
                return StatusCode(500, ex.Message);
            }
            catch (TwilioRequestException)
            {
                // Avoid returning upstream payload; keep it safe/clean.
                return StatusCode(502, "Falha ao enviar SMS via Twilio. Verifique a configuração e o número do destinatário.");
            }

        
        }


    }
}
