using Core.DTO.ServiceType;
using Core.Models;
using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.Security;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ServiceTypesController : ControllerBase
    {
        private readonly DbContextClass _db;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public ServiceTypesController(DbContextClass db, ICurrentUser currentUser, IScopeGuard scope)
        {
            _db = db;
            _currentUser = currentUser;
            _scope = scope;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? companyId, CancellationToken ct)
        {
            // Admin pode ver qualquer company; company/professional ficam restritos ao escopo
            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                companyId = scopedCompanyId;
            }

            var query = _db.ServiceTypes.AsQueryable();
            if (companyId.HasValue)
                query = query.Where(s => s.CompanyId == companyId.Value);

            var list = await query
                .OrderBy(s => s.Name)
                .Select(s => new ServiceTypeDTO
                {
                    Id = s.Id,
                    Name = s.Name,
                    CompanyId = s.CompanyId,
                    IsActive = s.IsActive
                })
                .ToListAsync(ct);

            return Ok(list);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var st = await _db.ServiceTypes.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (st == null) return NotFound();

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(st.CompanyId);

            return Ok(new ServiceTypeDTO
            {
                Id = st.Id,
                Name = st.Name,
                CompanyId = st.CompanyId,
                IsActive = st.IsActive
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateServiceTypeDTO dto, CancellationToken ct)
        {
            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (scopedCompanyId.HasValue) dto.CompanyId = scopedCompanyId.Value;
            }

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(dto.CompanyId);

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Name é obrigatório.");

            var st = new ServiceType
            {
                Name = dto.Name.Trim(),
                CompanyId = dto.CompanyId,
                IsActive = dto.IsActive
            };

            _db.ServiceTypes.Add(st);
            await _db.SaveChangesAsync(ct);

            return Ok(new ServiceTypeDTO
            {
                Id = st.Id,
                Name = st.Name,
                CompanyId = st.CompanyId,
                IsActive = st.IsActive
            });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateServiceTypeDTO dto, CancellationToken ct)
        {
            var st = await _db.ServiceTypes.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (st == null) return NotFound();

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(st.CompanyId);

            if (dto.Name != null) st.Name = dto.Name.Trim();
            if (dto.IsActive.HasValue) st.IsActive = dto.IsActive.Value;

            await _db.SaveChangesAsync(ct);

            return Ok(new ServiceTypeDTO
            {
                Id = st.Id,
                Name = st.Name,
                CompanyId = st.CompanyId,
                IsActive = st.IsActive
            });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var st = await _db.ServiceTypes.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (st == null) return NotFound();

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(st.CompanyId);

            // Se houver agendamentos usando, preferimos bloquear para evitar FK issue.
            var inUse = await _db.Appointments.AnyAsync(a => a.ServiceTypeId == id, ct);
            if (inUse)
                return BadRequest("ServiceType está em uso por Appointments.");

            _db.ServiceTypes.Remove(st);
            await _db.SaveChangesAsync(ct);

            return NoContent();
        }
    }
}
