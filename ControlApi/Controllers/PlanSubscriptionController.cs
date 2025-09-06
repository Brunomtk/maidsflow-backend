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
    }
}
