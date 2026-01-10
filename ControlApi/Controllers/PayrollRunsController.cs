using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using Services.Security;
using Core.DTO.Payroll;
using System.Threading.Tasks;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PayrollRunsController : ControllerBase
    {
        private readonly IPayrollRunService _service;
        private readonly IScopeGuard _scope;

        public PayrollRunsController(IPayrollRunService service, IScopeGuard scope)
        {
            _service = service;
            _scope = scope;
        }

        [HttpGet("company/{companyId}")]
        public async Task<ActionResult> ListByCompany(int companyId)
        {
            await _scope.EnsureCompanyAccessAsync(companyId);
            var result = await _service.ListByCompanyAsync(companyId);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult> GetDetails(int id)
        {
            var result = await _service.GetDetailsAsync(id);
            return Ok(result);
        }

        [HttpPost("company/{companyId}")]
        public async Task<ActionResult> Create(int companyId, [FromBody] CreatePayrollRunRequestDTO dto)
        {
            var result = await _service.CreateRunAsync(companyId, dto);
            return Ok(result);
        }

        [HttpPost("{id}/close")]
        public async Task<ActionResult> Close(int id)
        {
            var result = await _service.CloseAsync(id);
            return Ok(result);
        }

        [HttpPost("{id}/mark-paid")]
        public async Task<ActionResult> MarkPaid(int id)
        {
            var result = await _service.MarkPaidAsync(id);
            return Ok(result);
        }
    }
}
