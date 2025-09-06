using Core.DTO.Professional;
using Core.Models;
using Infrastructure.ServiceExtension;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProfessionalController : ControllerBase
    {
        private readonly IProfessionalService _professionalService;

        public ProfessionalController(IProfessionalService professionalService)
        {
            _professionalService = professionalService;
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
