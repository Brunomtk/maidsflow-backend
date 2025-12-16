using Microsoft.AspNetCore.Mvc;
using Services;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlanSubscriptionController : ControllerBase
    {
        private readonly IPlanSubscriptionService _subscriptionService;

        public PlanSubscriptionController(IPlanSubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        /// <summary>
        /// Lista assinantes de um plano com paginação.
        /// </summary>
        /// <param name="planId">ID do plano</param>
        /// <param name="page">Número da página</param>
        /// <param name="pageSize">Tamanho da página</param>
        [HttpGet("{planId}/subscribers")]
        public async Task<IActionResult> GetSubscribers(
            [FromRoute] int planId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (planId <= 0)
                return BadRequest("Parâmetro planId inválido.");

            var result = await _subscriptionService.GetSubscribersByPlan(planId, page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Retorna a assinatura ativa de uma Company.
        /// </summary>
        [HttpGet("company/{companyId}/active")]
        public async Task<IActionResult> GetActiveByCompany([FromRoute] int companyId)
        {
            if (companyId <= 0) return BadRequest("CompanyId inválido.");
            var result = await _subscriptionService.GetActiveByCompanyAsync(companyId);
            return Ok(result);
        }

        /// <summary>
        /// Lista todas as assinaturas de uma Company.
        /// </summary>
        [HttpGet("company/{companyId}")]
        public async Task<IActionResult> GetByCompany([FromRoute] int companyId)
        {
            if (companyId <= 0) return BadRequest("CompanyId inválido.");
            var result = await _subscriptionService.GetByCompanyAsync(companyId);
            return Ok(result);
        }

        /// <summary>
        /// Cancela uma assinatura (Status = Cancelled).
        /// </summary>
        [HttpPut("{subscriptionId}/cancel")]
        public async Task<IActionResult> Cancel([FromRoute] int subscriptionId)
        {
            if (subscriptionId <= 0) return BadRequest("subscriptionId inválido.");
            var ok = await _subscriptionService.CancelAsync(subscriptionId);
            return ok ? Ok("Assinatura cancelada.") : NotFound("Assinatura não encontrada.");
        }
    }
}
