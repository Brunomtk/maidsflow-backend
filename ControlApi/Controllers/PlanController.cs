using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Core.DTO;
using Services;
using Core.DTO.Company;
using Core.DTO.Plan;
using Core.Models;

namespace Api.Controllers
{
    [ApiController]
[Authorize]
    [Route("api/[controller]")]
    public class PlanController : ControllerBase
    {
        private readonly IPlanService _planService;
        private readonly IPlanSubscriptionService _subscriptionService;

        public PlanController(IPlanService planService, IPlanSubscriptionService subscriptionService)
        {
            _planService = planService;
            _subscriptionService = subscriptionService;
        }

        /// <summary>
        /// Retorna todos os planos (sem paginaÃ§Ã£o).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllPlans()
        {
            var plans = await _planService.GetAllPlans();
            return Ok(plans);
        }

        /// <summary>
        /// Retorna os planos paginados com filtros opcionais.
        /// </summary>
        [HttpGet("paged")]
        public async Task<IActionResult> GetPagedPlans([FromQuery] FiltersDTO filtersDTO)
        {
            var result = await _planService.GetPlansPaged(filtersDTO);
            return Ok(result);
        }

        /// <summary>
        /// Retorna um plano por ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPlanById(int id)
        {
            var plan = await _planService.GetPlanById(id);
            return plan == null ? NotFound("Plano nÃ£o encontrado.") : Ok(plan);
        }

        /// <summary>
        /// Cria um novo plano.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreatePlan([FromBody] CreatePlanRequest request)
        {
            if (request == null) return BadRequest("Payload inválido.");

            var plan = new Plan
            {
                Name = request.Name,
                Price = request.Price,
                Duration = request.Duration,
                Features = request.Features ?? new List<string>(),
                Status = Core.Enums.StatusEnum.Active,
                ProfessionalsLimit = request.Limits?.Professionals,
                TeamsLimit = request.Limits?.Teams,
                CustomersLimit = request.Limits?.Customers,
                AppointmentsLimit = request.Limits?.Appointments
            };

            var created = await _planService.CreatePlan(plan);
            return created ? Ok("Plano criado com sucesso.") : BadRequest("Erro ao criar plano.");
        }

        /// <summary>
        /// Atualiza um plano existente.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePlan(int id, [FromBody] UpdatePlanRequest request)
        {
            if (request == null) return BadRequest("Payload inválido.");

            var plan = await _planService.GetPlanById(id);
            if (plan == null) return NotFound("Plano não encontrado.");

            // Atualiza apenas o que veio no payload
            if (!string.IsNullOrWhiteSpace(request.Name)) plan.Name = request.Name;
            if (request.Price.HasValue) plan.Price = request.Price.Value;
            if (request.Duration.HasValue) plan.Duration = request.Duration.Value;
            if (request.Features != null) plan.Features = request.Features;

            if (request.Limits != null)
            {
                plan.ProfessionalsLimit = request.Limits.Professionals;
                plan.TeamsLimit = request.Limits.Teams;
                plan.CustomersLimit = request.Limits.Customers;
                plan.AppointmentsLimit = request.Limits.Appointments;
            }

            var updated = await _planService.UpdatePlan(plan);
            return updated ? Ok("Plano atualizado com sucesso.") : BadRequest("Erro ao atualizar plano.");
        }

        /// <summary>
        /// Atualiza o status de um plano.
        /// </summary>
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusDTO dto)
        {
            var updated = await _planService.UpdateStatus(id, dto.Status);
            return updated ? Ok("Status atualizado com sucesso.") : NotFound("Plano nÃ£o encontrado.");
        }

        /// <summary>
        /// Deleta um plano.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePlan(int id)
        {
            var deleted = await _planService.DeletePlan(id);
            return deleted ? Ok("Plano deletado com sucesso.") : NotFound("Plano nÃ£o encontrado.");
        }

        /// <summary>
        /// Ativa um plano para uma Company. A duraÃ§Ã£o Ã© calculada a partir da data de ativaÃ§Ã£o (UTC) com base no campo Duration do plano.
        /// Se existir uma assinatura ativa para essa Company, ela serÃ¡ marcada como Inactive.
        /// </summary>
        [HttpPost("{id}/activate")]
        public async Task<IActionResult> ActivatePlan([FromRoute] int id, [FromBody] ActivatePlanDTO dto)
        {
            if (id <= 0) return BadRequest("ID do plano invÃ¡lido.");
            if (dto.CompanyId <= 0) return BadRequest("CompanyId invÃ¡lido.");

            var subscription = await _subscriptionService.ActivateAsync(id, dto.CompanyId, dto.AutoRenew);
            return Ok(subscription);
        }

        /// <summary>
        /// Ativa um plano em modo teste por 15 dias para uma Company.
        /// A assinatura criada sempre terÃ¡ AutoRenew=false e EndDate=StartDate+15 dias.
        /// Se existir uma assinatura ativa para essa Company, ela serÃ¡ marcada como Inactive e encerrada imediatamente.
        /// </summary>
        [HttpPost("{id}/activate-trial-15-days")]
        public async Task<IActionResult> ActivatePlanTrial15Days([FromRoute] int id, [FromBody] ActivatePlanDTO dto)
        {
            if (id <= 0) return BadRequest("ID do plano invÃ¡lido.");
            if (dto.CompanyId <= 0) return BadRequest("CompanyId invÃ¡lido.");

            var subscription = await _subscriptionService.ActivateTrial15DaysAsync(id, dto.CompanyId);
            return Ok(subscription);
        }

        /// <summary>
        /// Lista as Companies assinantes de um plano (com status e datas).
        /// </summary>
        [HttpGet("{id}/companies")]
        public async Task<IActionResult> GetPlanCompanies([FromRoute] int id)
        {
            var plan = await _planService.GetPlanById(id);
            if (plan == null) return NotFound("Plano nÃ£o encontrado.");

            // devolve as assinaturas jÃ¡ com Company incluÃ­da (via repository)
            return Ok(plan.Subscriptions);
        }
    }
}
