using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using Services.Security;
using Core.DTO.Payroll;
using Core.Exceptions;
using System;
using System.Threading.Tasks;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PayrollController : ControllerBase
    {
        private readonly IPayrollPreviewService _preview;
        private readonly IScopeGuard _scope;

        public PayrollController(IPayrollPreviewService preview, IScopeGuard scope)
        {
            _preview = preview;
            _scope = scope;
        }

        /// <summary>
        /// Preview do payroll por período (não persiste no banco nesta fase).
        /// Retorna itens por profissional e um resumo por profissional.
        /// </summary>
        [HttpGet("preview/company/{companyId}")]
        public async Task<ActionResult<PayrollPreviewResponseDTO>> PreviewCompany(
            int companyId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            if (!from.HasValue || !to.HasValue)
                throw new BadRequestException("Parâmetros 'from' e 'to' são obrigatórios.");

            // Segurança: Company só acessa a própria; Admin pode tudo.
            await _scope.EnsureCompanyAccessAsync(companyId);

            var result = await _preview.PreviewCompanyAsync(companyId, from.Value, to.Value);
            return Ok(result);
        }
    }
}
