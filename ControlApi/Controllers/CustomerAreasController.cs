using Core.DTO.Checklist;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CustomerAreasController : ControllerBase
    {
        private readonly Services.ICustomerAreaService _service;
        public CustomerAreasController(Services.ICustomerAreaService service) => _service = service;

        [HttpGet("by-customer/{customerId:int}")]
        public IActionResult GetByCustomer(int customerId, [FromQuery] bool onlyActive = true)
        {
            var q = _service.QueryByCustomer(customerId, onlyActive);
            return Ok(q.ToList());
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCustomerAreaDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var area = await _service.CreateAsync(dto);
            return Ok(area);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateCustomerAreaDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var ok = await _service.UpdateAsync(dto);
            return ok ? Ok() : NotFound();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await _service.SoftDeleteAsync(id);
            return ok ? Ok() : NotFound();
        }
    }
}
