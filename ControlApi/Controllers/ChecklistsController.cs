using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Enums;
using Core.DTO.Checklist;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Infrastructure;
using Services;
using Services.Storage;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChecklistsController : ControllerBase
    {
        private readonly IChecklistService _service;
        private readonly DbContextClass _db;
        private readonly IS3StorageService _s3;

        public ChecklistsController(IChecklistService service, DbContextClass db, IS3StorageService s3)
        {
            _service = service;
            _db = db;
            _s3 = s3;
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

        /// <summary>
        /// Gera URL pré-assinada (PUT) para upload de foto de Checklist Item para S3.
        /// O backend retorna a Key (que deve ser salva no campo Url da foto) e o UploadUrl.
        /// </summary>
        [HttpPost("items/photos/presign")]
        public async Task<IActionResult> PresignChecklistItemPhoto([FromBody] PresignChecklistItemPhotoDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var item = await _db.ChecklistItems.AsNoTracking()
                .Include(i => i.Checklist)
                .FirstOrDefaultAsync(i => i.Id == dto.ItemId);

            if (item == null || item.Checklist == null)
                return NotFound("Checklist item não encontrado.");

            var contentType = string.IsNullOrWhiteSpace(dto.ContentType) ? "application/octet-stream" : dto.ContentType;
            var presigned = _s3.CreateChecklistPhotoUploadUrl(item.ChecklistId, item.Id, dto.FileName, contentType!);

            return Ok(new PresignChecklistItemPhotoResponseDTO
            {
                Key = presigned.Key,
                UploadUrl = presigned.UploadUrl,
                ExpiresAtUnixSeconds = presigned.ExpiresAt.ToUnixTimeSeconds()
            });
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
                CompanyId = ck.Appointment != null ? ck.Appointment.CompanyId : ck.Customer.CompanyId,
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
                    Url = _s3.TryGetKeyFromStoredValue(p.Url, out var key)
                        ? _s3.CreateDownloadUrl(key)
                        : p.Url,
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

    public class ChecklistDetailsDTO
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public ChecklistStatus Status { get; set; }
        public string? ObservacoesGerais { get; set; }
        public DateTime CreatedDate { get; set; }
        public CustomerSummaryDTO Customer { get; set; } = new();
        public AppointmentSummaryDTO? Appointment { get; set; }
        public List<ChecklistDetailsItemDTO> Items { get; set; } = new();
        public List<ChecklistDetailsAreaDTO> Areas { get; set; } = new();
    }

    public class CustomerSummaryDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class ChecklistDetailsItemDTO
    {
        public int Id { get; set; }
        public int? CustomerAreaId { get; set; }
        public string CustomerAreaName { get; set; } = string.Empty;
        public ChecklistItemStatus? Status { get; set; }
        public string? Observacoes { get; set; }
        public List<ChecklistDetailsPhotoDTO> Photos { get; set; } = new();
    }

    public class ChecklistDetailsPhotoDTO
    {
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? Descricao { get; set; }
    }

    public class ChecklistDetailsAreaDTO
    {
        public int? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<ChecklistDetailsItemDTO> Items { get; set; } = new();
    }

    public class AppointmentSummaryDTO
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

}