using Core.DTO.Checklist;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChecklistsController : ControllerBase
    {
        private readonly IChecklistService _service;
        public ChecklistsController(IChecklistService service) => _service = service;

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
            var page = await _service.GetPagedAsync(filters);
            return Ok(page);
        }

        [HttpPut("items")]
        public async Task<IActionResult> UpdateItem([FromBody] UpdateChecklistItemDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var ok = await _service.UpdateItemAsync(dto);
            return ok ? Ok() : NotFound();
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
        public async Task<IActionResult> UpdateMeta(int id, [FromBody] Core.DTO.Checklist.UpdateChecklistMetaDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (dto.ChecklistId == 0) dto.ChecklistId = id;
            var ok = await _service.UpdateMetaAsync(dto);
            return ok ? Ok() : NotFound();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await _service.DeleteAsync(id);
            return ok ? Ok() : NotFound();
        }
    }
}
