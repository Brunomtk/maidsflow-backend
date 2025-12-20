using Microsoft.AspNetCore.Mvc;
using Services;
using Core.DTO.Plan;
using Core.Enums.Plan;

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

        private static bool TryParseStatus(string? input, out PlanSubscriptionStatusEnum status)
        {
            status = default;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            // aceita número (ex: "0")
            if (int.TryParse(input, out var n) && Enum.IsDefined(typeof(PlanSubscriptionStatusEnum), n))
            {
                status = (PlanSubscriptionStatusEnum)n;
                return true;
            }

            // aceita nome (ex: "Cancelled")
            return Enum.TryParse(input, true, out status);
        }

        /// <summary>
        /// Retorna assinantes (companies) de um plano (paginado).
        /// </summary>
        [HttpGet("{planId}/subscribers")]
        public async Task<IActionResult> GetSubscribers([FromRoute] int planId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
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
        /// Ativa um plano para uma Company (cria uma nova assinatura Active e desativa qualquer outra Active).
        /// OBS: Plan.Duration é em DIAS.
        /// </summary>
        [HttpPost("activate")]
        public async Task<IActionResult> Activate([FromBody] ActivatePlanSubscriptionRequest req)
        {
            if (req == null) return BadRequest("Body inválido.");
            if (req.PlanId <= 0) return BadRequest("PlanId inválido.");
            if (req.CompanyId <= 0) return BadRequest("CompanyId inválido.");

            var subscription = await _subscriptionService.ActivateAsync(req.PlanId, req.CompanyId, req.AutoRenew, req.StartDateUtc, req.EndDateUtc);
            return Ok(subscription);
        }

        /// <summary>
        /// Ativa uma assinatura existente (Status = Active), desativando outras da mesma Company.
        /// Pode opcionalmente ajustar datas e AutoRenew.
        /// </summary>
        [HttpPut("{subscriptionId}/activate")]
        public async Task<IActionResult> ActivateExisting([FromRoute] int subscriptionId, [FromBody] ActivateExistingPlanSubscriptionRequest? req)
        {
            if (subscriptionId <= 0) return BadRequest("subscriptionId inválido.");

            var subscription = await _subscriptionService.ActivateExistingAsync(
                subscriptionId,
                req?.StartDateUtc,
                req?.EndDateUtc,
                req?.AutoRenew
            );

            if (subscription == null) return NotFound("Assinatura não encontrada.");
            return Ok(subscription);
        }

        /// <summary>
        /// Atualiza uma assinatura: StartDateUtc / EndDateUtc / AutoRenew / Status.
        /// Status aqui altera o campo Status do PlanSubscription (não do Plan).
        /// </summary>
        [HttpPut("{subscriptionId}")]
        public async Task<IActionResult> Update([FromRoute] int subscriptionId, [FromBody] UpdatePlanSubscriptionRequest req)
        {
            if (subscriptionId <= 0) return BadRequest("subscriptionId inválido.");
            if (req == null) return BadRequest("Body inválido.");

            PlanSubscriptionStatusEnum? status = null;
            if (!string.IsNullOrWhiteSpace(req.Status))
            {
                if (!TryParseStatus(req.Status, out var parsed))
                    return BadRequest("Status inválido. Use: 0=Active, 1=Inactive, 2=Expired, 3=Cancelled (ou o nome).");

                status = parsed;
            }

            var updated = await _subscriptionService.UpdateAsync(
                subscriptionId,
                req.StartDateUtc,
                req.EndDateUtc,
                req.AutoRenew,
                status
            );

            if (updated == null) return NotFound("Assinatura não encontrada.");
            return Ok(updated);
        }

        /// <summary>
        /// Troca o plano de uma Company (cria nova assinatura Active e desativa qualquer outra Active).
        /// </summary>
        [HttpPost("company/{companyId}/switch-plan")]
        public async Task<IActionResult> SwitchPlan([FromRoute] int companyId, [FromBody] SwitchCompanyPlanRequest req)
        {
            if (companyId <= 0) return BadRequest("CompanyId inválido.");
            if (req == null) return BadRequest("Body inválido.");
            if (req.PlanId <= 0) return BadRequest("PlanId inválido.");

            var subscription = await _subscriptionService.SwitchPlanAsync(companyId, req.PlanId, req.AutoRenew, req.StartDateUtc, req.EndDateUtc);
            return Ok(subscription);
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
