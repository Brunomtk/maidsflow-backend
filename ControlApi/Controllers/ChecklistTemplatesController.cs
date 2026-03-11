using System.Threading.Tasks;
using Core.DTO.ChecklistTemplate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChecklistTemplatesController : ControllerBase
    {
        private readonly IChecklistTemplateService _service;

        public ChecklistTemplatesController(IChecklistTemplateService service)
        {
            _service = service;
        }

        [HttpPost("seed-defaults")]
        public async Task<IActionResult> SeedDefaults()
        {
            await _service.SeedDefaultAirbnbTemplatesAsync();
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var templates = await _service.GetVisibleTemplatesAsync();
            return Ok(templates);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var template = await _service.GetByIdAsync(id);
            return template != null ? Ok(template) : NotFound();
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateChecklistTemplateDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var created = await _service.CreateAsync(dto);
            return Ok(created);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateChecklistTemplateDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            dto.Id = id;
            var updated = await _service.UpdateAsync(dto);
            return updated != null ? Ok(updated) : NotFound();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            return deleted ? Ok() : NotFound();
        }
    }
}
