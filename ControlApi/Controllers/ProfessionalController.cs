using Core.DTO.Appointment;
using Core.DTO.Professional;
using Core.Enums.Appointment;
using Core.Models;
using Infrastructure.ServiceExtension;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProfessionalController : ControllerBase
    {
        private readonly IProfessionalService _professionalService;
        private readonly IAppointmentService _appointmentService;

        public ProfessionalController(IProfessionalService professionalService, IAppointmentService appointmentService)
        {
            _professionalService = professionalService;
            _appointmentService = appointmentService;
        }

        /// <summary>
        /// Lista os agendamentos associados a um profissional, com filtros por status.
        /// Útil para a área "completedservices" no app do Professional.
        ///
        /// status aceitos (case-insensitive):
        /// - scheduled | schedule
        /// - inprogress | in_progress
        /// - cancelled | canceled
        /// - completed
        /// - all (ou vazio) => não filtra por status
        /// </summary>
        [HttpGet("{id:int}/completedservices")]
        public async Task<ActionResult<PagedResult<AppointmentDTO>>> GetCompletedServices(
            int id,
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var filters = new AppointmentFiltersDTO
            {
                ProfessionalId = id,
                Page = page <= 0 ? 1 : page,
                PageSize = pageSize is <= 0 or > 200 ? 20 : pageSize,
                StartDate = startDate,
                EndDate = endDate
            };

            var parsedStatus = TryParseAppointmentStatus(status);
            if (parsedStatus == null && !string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Status inválido. Use: scheduled, inprogress, cancelled/canceled, completed ou all.");
            }

            if (parsedStatus.HasValue)
                filters.Status = parsedStatus.Value;

            var paged = await _appointmentService.GetPagedAppointments(filters);

            var mapped = new PagedResult<AppointmentDTO>
            {
                CurrentPage = paged.CurrentPage,
                PageCount = paged.PageCount,
                PageSize = paged.PageSize,
                TotalItems = paged.TotalItems,
                Results = paged.Results.Select(a => new AppointmentDTO
                {
                    Id = a.Id,
                    Title = a.Title,
                    Address = a.Address,
                    Start = a.Start,
                    End = a.End,
                    Status = a.Status.ToString(),
                    Type = a.Type.ToString(),
                    Notes = a.Notes,
                    CompanyId = a.CompanyId,
                    CompanyName = a.Company?.Name,
                    CustomerId = a.CustomerId,
                    CustomerName = a.Customer?.Name,
                    CustomerTicket = a.Customer?.Ticket,
                    TeamId = a.TeamId,
                    TeamName = a.Team?.Name,
                    ProfessionalIds = a.ProfessionalIds,

                    TimeZoneId = a.TimeZoneId,
                    IsRecurring = a.IsRecurring,
                    RecurrenceRule = a.RecurrenceRule,
                    SeriesId = a.SeriesId,
                    RecurrenceEnd = a.RecurrenceEnd,
                    OccurrenceCount = a.OccurrenceCount,
                    IsException = a.IsException,
                    OriginalStart = a.OriginalStart,
                    OriginalEnd = a.OriginalEnd
                }).ToList()
            };

            return Ok(mapped);
        }

        private static AppointmentStatus? TryParseAppointmentStatus(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var v = raw.Trim().ToLowerInvariant();

            // Normalizações de escrita
            if (v == "schedule") v = "scheduled";
            if (v == "in_progress") v = "inprogress";
            if (v == "canceled") v = "cancelled"; // enum interno é "Cancelled"

            if (v == "all") return null;

            return v switch
            {
                "scheduled" => AppointmentStatus.Scheduled,
                "inprogress" => AppointmentStatus.InProgress,
                "completed" => AppointmentStatus.Completed,
                "cancelled" => AppointmentStatus.Cancelled,
                _ => null
            };
        }

        /// <summary>
        /// Returns all professionals without pagination.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProfessionalDTO>>> GetAll()
        {
            var professionals = await _professionalService.GetAllProfessionals();
            var response = professionals.Select(p => new ProfessionalDTO
            {
                Id = p.Id,
                Name = p.Name,
                Cpf = p.Cpf,
                Email = p.Email,
                Phone = p.Phone,
                TeamId = p.TeamId,
                CompanyId = p.CompanyId,
                Status = p.Status.ToString(),
                Rating = p.Rating,
                CompletedServices = p.CompletedServices,
                CreatedAt = p.CreatedDate,
                UpdatedAt = p.UpdatedDate
            });

            return Ok(response);
        }

        /// <summary>
        /// Returns a professional by ID.
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ProfessionalDTO>> GetById(int id)
        {
            var professional = await _professionalService.GetProfessionalById(id);
            if (professional == null)
                return NotFound("Professional not found.");

            var dto = new ProfessionalDTO
            {
                Id = professional.Id,
                Name = professional.Name,
                Cpf = professional.Cpf,
                Email = professional.Email,
                Phone = professional.Phone,
                TeamId = professional.TeamId,
                CompanyId = professional.CompanyId,
                Status = professional.Status.ToString(),
                Rating = professional.Rating,
                CompletedServices = professional.CompletedServices,
                CreatedAt = professional.CreatedDate,
                UpdatedAt = professional.UpdatedDate
            };

            return Ok(dto);
        }

        /// <summary>
        /// Creates a new professional.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ProfessionalDTO>> Create([FromBody] CreateProfessionalRequest request)
        {
            if (!ModelState.IsValid || request == null)
                return BadRequest(ModelState);

            var created = await _professionalService.CreateProfessional(request);

            var dto = new ProfessionalDTO
            {
                Id = created.Id,
                Name = created.Name,
                Cpf = created.Cpf,
                Email = created.Email,
                Phone = created.Phone,
                TeamId = created.TeamId,
                CompanyId = created.CompanyId,
                Status = created.Status.ToString(),
                Rating = created.Rating,
                CompletedServices = created.CompletedServices,
                CreatedAt = created.CreatedDate,
                UpdatedAt = created.UpdatedDate
            };

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, dto);
        }

        /// <summary>
        /// Updates an existing professional by ID.
        /// </summary>
        [HttpPut("{id:int}")]
        public async Task<ActionResult<ProfessionalDTO>> Update(int id, [FromBody] UpdateProfessionalRequest request)
        {
            if (!ModelState.IsValid || request == null)
                return BadRequest(ModelState);

            var updated = await _professionalService.UpdateProfessional(id, request);
            if (updated == null)
                return NotFound("Professional not found.");

            var dto = new ProfessionalDTO
            {
                Id = updated.Id,
                Name = updated.Name,
                Cpf = updated.Cpf,
                Email = updated.Email,
                Phone = updated.Phone,
                TeamId = updated.TeamId,
                CompanyId = updated.CompanyId,
                Status = updated.Status.ToString(),
                Rating = updated.Rating,
                CompletedServices = updated.CompletedServices,
                CreatedAt = updated.CreatedDate,
                UpdatedAt = updated.UpdatedDate
            };

            return Ok(dto);
        }

        /// <summary>
        /// Deletes a professional by ID.
        /// </summary>
        [HttpDelete("{id:int}")]
        public async Task<ActionResult> Delete(int id)
        {
            var success = await _professionalService.DeleteProfessional(id);
            if (!success)
                return NotFound("Professional not found.");

            return NoContent();
        }

        /// <summary>
        /// Returns paginated professionals with optional filters.
        /// </summary>
        [HttpGet("paged")]
        public async Task<ActionResult<PagedResult<ProfessionalDTO>>> GetPaged([FromQuery] ProfessionalFiltersDTO filters)
        {
            var paged = await _professionalService.GetPagedProfessionals(filters);

            var response = new PagedResult<ProfessionalDTO>
            {
                CurrentPage = paged.CurrentPage,
                PageCount = paged.PageCount,
                PageSize = paged.PageSize,
                TotalItems = paged.TotalItems,
                Results = paged.Results.Select(p => new ProfessionalDTO
                {
                    Id = p.Id,
                    Name = p.Name,
                    Cpf = p.Cpf,
                    Email = p.Email,
                    Phone = p.Phone,
                    TeamId = p.TeamId,
                    CompanyId = p.CompanyId,
                    Status = p.Status.ToString(),
                    Rating = p.Rating,
                    CompletedServices = p.CompletedServices,
                    CreatedAt = p.CreatedDate,
                    UpdatedAt = p.UpdatedDate
                }).ToList()
            };

            return Ok(response);
        }
    }
}
