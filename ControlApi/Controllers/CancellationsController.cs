// ControlApi/Controllers/CancellationsController.cs
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Core.DTO.Cancellation;
using Infrastructure.ServiceExtension;  // <-- para o PagedResult<T>
using Services;
using Core.Models;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CancellationsController : ControllerBase
    {
        private readonly ICancellationService _service;

        public CancellationsController(ICancellationService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<Cancellation>>> GetAll([FromQuery] CancellationFiltersDto filters)
        {
            var page = await _service.GetAllAsync(filters);
            return Ok(page);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Cancellation>> GetById(int id)
        {
            var item = await _service.GetByIdAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost]
        public async Task<ActionResult<Cancellation>> Create([FromBody] CreateCancellationDto dto)
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Cancellation>> Update(int id, [FromBody] UpdateCancellationDto dto)
        {
            var updated = await _service.UpdateAsync(id, dto);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await _service.DeleteAsync(id)) return NotFound();
            return NoContent();
        }

        [HttpPost("{id:int}/refund")]
        public async Task<ActionResult<Cancellation>> ProcessRefund(int id, [FromBody] ProcessRefundDto dto)
        {
            var result = await _service.ProcessRefundAsync(id, dto);
            if (result == null) return NotFound();
            return Ok(result);
        }
    }
}
