using Core.DTO.Recurrences;
using Infrastructure.ServiceExtension;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using System.Threading.Tasks;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RecurrenceController : ControllerBase
    {
        private readonly IRecurrenceService _recurrenceService;

        public RecurrenceController(IRecurrenceService recurrenceService)
        {
            _recurrenceService = recurrenceService;
        }

        /// <summary>
        /// Lista recorrências com filtros e paginação.
        /// </summary>
        /// <param name="filters">Filtros aplicáveis para busca.</param>
        [HttpGet]
        public async Task<IActionResult> GetPaged([FromQuery] RecurrenceFiltersDTO filters)
        {
            var result = await _recurrenceService.GetPagedAsync(filters);
            return Ok(result);
        }

        /// <summary>
        /// Retorna uma recorrência específica pelo ID.
        /// </summary>
        /// <param name="id">ID da recorrência.</param>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var recurrence = await _recurrenceService.GetByIdAsync(id);
            if (recurrence == null) return NotFound();
            return Ok(recurrence);
        }

        /// <summary>
        /// Cria uma nova recorrência.
        /// </summary>
        /// <param name="dto">Dados da recorrência.</param>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRecurrenceDTO dto)
        {
            var created = await _recurrenceService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        /// <summary>
        /// Atualiza uma recorrência existente.
        /// </summary>
        /// <param name="id">ID da recorrência.</param>
        /// <param name="dto">Dados atualizados.</param>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateRecurrenceDTO dto)
        {
            var updated = await _recurrenceService.UpdateAsync(id, dto);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        /// <summary>
        /// Remove uma recorrência.
        /// </summary>
        /// <param name="id">ID da recorrência.</param>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _recurrenceService.DeleteAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }
    }
}
