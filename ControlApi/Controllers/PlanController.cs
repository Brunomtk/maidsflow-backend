using Microsoft.AspNetCore.Mvc;
using Core.DTO;
using Services;
using Core.DTO.Company;
using Core.Models;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlanController : ControllerBase
    {
        private readonly IPlanService _planService;

        public PlanController(IPlanService planService)
        {
            _planService = planService;
        }

        /// <summary>
        /// Retorna todos os planos (sem paginação).
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
            return plan == null ? NotFound("Plano não encontrado.") : Ok(plan);
        }

        /// <summary>
        /// Cria um novo plano.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreatePlan([FromBody] Plan plan)
        {
            var created = await _planService.CreatePlan(plan);
            return created ? Ok("Plano criado com sucesso.") : BadRequest("Erro ao criar plano.");
        }

        /// <summary>
        /// Atualiza um plano existente.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePlan(int id, [FromBody] Plan plan)
        {
            if (id != plan.Id) return BadRequest("ID do plano inválido.");
            var updated = await _planService.UpdatePlan(plan);
            return updated ? Ok("Plano atualizado com sucesso.") : NotFound("Plano não encontrado.");
        }

        /// <summary>
        /// Atualiza o status de um plano.
        /// </summary>
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusDTO dto)
        {
            var updated = await _planService.UpdateStatus(id, dto.Status);
            return updated ? Ok("Status atualizado com sucesso.") : NotFound("Plano não encontrado.");
        }

        /// <summary>
        /// Deleta um plano.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePlan(int id)
        {
            var deleted = await _planService.DeletePlan(id);
            return deleted ? Ok("Plano deletado com sucesso.") : NotFound("Plano não encontrado.");
        }
    }
}
