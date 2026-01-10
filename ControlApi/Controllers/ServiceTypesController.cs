using Core.DTO.ServiceTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ServiceTypesController : ControllerBase
    {
        private readonly IServiceTypeService _service;

        public ServiceTypesController(IServiceTypeService service)
        {
            _service = service;
        }

        [HttpGet("company/{companyId:int}")]
        public async Task<IActionResult> GetByCompany(int companyId, [FromQuery] bool includeInactive = false)
        {
            var list = await _service.GetByCompanyAsync(companyId, includeInactive);
            return Ok(list);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var st = await _service.GetByIdAsync(id);
            if (st == null) return NotFound();
            return Ok(st);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateServiceTypeDTO dto)
        {
            var created = await _service.CreateAsync(dto);
            return Ok(created);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateServiceTypeDTO dto)
        {
            var updated = await _service.UpdateAsync(id, dto);
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await _service.DeleteAsync(id);
            if (!ok) return NotFound();
            return NoContent();
        }
    }
}
