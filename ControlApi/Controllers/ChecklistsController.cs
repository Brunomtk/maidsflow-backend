using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO.Checklist;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Infrastructure;
using Services;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChecklistsController : ControllerBase
    {
        private readonly IChecklistService _service;
        private readonly DbContextClass _db;

        public ChecklistsController(IChecklistService service, DbContextClass db)
        {
            _service = service;
            _db = db;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateChecklistDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var ck = await _service.CreateAsync(dto);
            return Ok(ck);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var ck = await _service.GetByIdAsync(id);
            return ck != null ? Ok(ck) : NotFound();
        }

        [HttpGet]
        public async Task<IActionResult> GetPaged([FromQuery] ChecklistFiltersDTO filters)
        {
            var paged = await _service.GetPagedAsync(filters);
            return Ok(paged);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await _service.DeleteAsync(id);
            return ok ? Ok() : NotFound();
        }

        [HttpPut("items")]
public async Task<IActionResult> UpdateItems([FromBody] List<UpdateChecklistItemDTO> dtos)
{
    if (!ModelState.IsValid) return BadRequest(ModelState);
    if (dtos == null || dtos.Count == 0) return BadRequest("Lista vazia.");

    var updated = 0;
    foreach (var dto in dtos)
    {
        var ok = await _service.UpdateItemAsync(dto);
        if (ok) updated++;
    }
    return updated > 0 ? Ok(new { updated }) : NotFound();
}
[HttpPost("items/photos")]
        public async Task<IActionResult> AddPhotos([FromBody] AddChecklistItemPhotosDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var ok = await _service.AddPhotosAsync(dto);
            return ok ? Ok() : NotFound();
        }

        [HttpDelete("items/photos/{photoId:int}")]
        public async Task<IActionResult> RemovePhoto(int photoId)
        {
            var ok = await _service.RemovePhotoAsync(photoId);
            return ok ? Ok() : NotFound();
        }

        [HttpPost("{id:int}/concluir")]
        public async Task<IActionResult> Conclude(int id)
        {
            var ok = await _service.ConcludeAsync(id);
            return ok ? Ok() : NotFound();
        }

        [HttpPut("{id:int}/meta")]
        public async Task<IActionResult> UpdateMeta(int id, [FromBody] UpdateChecklistMetaDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            dto.ChecklistId = id;
            var ok = await _service.UpdateMetaAsync(dto);
            return ok ? Ok() : NotFound();
        }

        [HttpPost("{id:int}/items")]
        public async Task<IActionResult> AddItem(int id, [FromBody] CreateChecklistItemDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            dto.ChecklistId = id;
            var newItemId = await _service.AddItemAsync(dto);
            if (newItemId <= 0) return NotFound();
            return Ok(new { itemId = newItemId });
        }

        [HttpPost("{id:int}/ensure-items")]
        public async Task<IActionResult> EnsureItems(int id)
        {
            var created = await _service.EnsureItemsFromAreasAsync(id);
            return Ok(new { created });
        }

        // ===== Details endpoint (Areas + Items + Observações + Fotos) =====
        [HttpGet("{id:int}/details")]
        public async Task<IActionResult> GetDetails(int id)
        {
            var ck = await _db.Checklists
                .Include(c => c.Customer)
                .Include(c => c.Appointment)
                .Include(c => c.Items).ThenInclude(i => i.Photos)
                .Include(c => c.Items).ThenInclude(i => i.CustomerArea)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (ck == null) return NotFound();

            var dto = new ChecklistDetailsDTO
            {
                Id = ck.Id,
                CompanyId = ck.CompanyId,
                Status = ck.Status,
                ObservacoesGerais = ck.ObservacoesGerais,
                CreatedDate = ck.CreatedDate,
                Customer = new CustomerSummaryDTO
                {
                    Id = ck.CustomerId,
                    Name = ck.Customer?.Name ?? string.Empty
                },
                Appointment = ck.AppointmentId.HasValue && ck.Appointment != null
                    ? new AppointmentSummaryDTO
                    {
                        Id = ck.Appointment.Id,
                        Title = ck.Appointment.Title,
                        Start = ck.Appointment.Start,
                        End = ck.Appointment.End
                    }
                    : null
            };

            dto.Items = ck.Items.Select(i => new ChecklistDetailsItemDTO
            {
                Id = i.Id,
                CustomerAreaId = i.CustomerAreaId,
                CustomerAreaName = i.CustomerArea?.Name ?? string.Empty,
                Status = i.Status,
                Observacoes = i.Observacoes,
                Photos = i.Photos.Select(p => new ChecklistDetailsPhotoDTO
                {
                    Id = p.Id,
                    Url = p.Url,
                    Descricao = p.Descricao
                }).ToList()
            }).ToList();

            dto.Areas = dto.Items
                .GroupBy(i => new { i.CustomerAreaId, i.CustomerAreaName })
                .Select(g => new ChecklistDetailsAreaDTO
                {
                    Id = g.Key.CustomerAreaId,
                    Name = g.Key.CustomerAreaName,
                    Items = g.ToList()
                })
                .OrderBy(a => a.Name)
                .ToList();

            return Ok(dto);
        }
    }
}
