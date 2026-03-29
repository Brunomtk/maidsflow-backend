using Core.DTO.Payments;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace ControlApi.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class PaymentCategoriesController : ControllerBase
    {
        private readonly IPaymentCategoryService _service;

        public PaymentCategoriesController(IPaymentCategoryService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<List<PaymentCategory>>> GetAll([FromQuery] bool includeInactive = false)
            => Ok(await _service.GetAllAsync(includeInactive));

        [HttpGet("{id:int}")]
        public async Task<ActionResult<PaymentCategory>> GetById(int id)
        {
            var entity = await _service.GetByIdAsync(id);
            if (entity == null) return NotFound();
            return Ok(entity);
        }

        [HttpPost]
        public async Task<ActionResult<PaymentCategory>> Post([FromBody] CreatePaymentCategoryDto dto)
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<PaymentCategory>> Put(int id, [FromBody] UpdatePaymentCategoryDto dto)
        {
            var updated = await _service.UpdateAsync(id, dto);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }
    }
}
