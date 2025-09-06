using Core.DTO.CheckRecord;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using System.Threading.Tasks;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CheckRecordController : ControllerBase
    {
        private readonly ICheckRecordService _checkRecordService;

        public CheckRecordController(ICheckRecordService checkRecordService)
        {
            _checkRecordService = checkRecordService;
        }

        /// <summary>
        /// Lista registros de check-in/check-out com filtros e paginação.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPaged([FromQuery] CheckRecordFiltersDTO filters)
        {
            var result = await _checkRecordService.GetPagedAsync(filters);
            return Ok(result);
        }

        /// <summary>
        /// Retorna um registro específico por ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var record = await _checkRecordService.GetByIdAsync(id);
            if (record == null)
                return NotFound();

            return Ok(record);
        }

        /// <summary>
        /// Cria um novo registro com status pendente.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCheckRecordDTO dto)
        {
            var created = await _checkRecordService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        /// <summary>
        /// Atualiza um registro existente.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateCheckRecordDTO dto)
        {
            var updated = await _checkRecordService.UpdateAsync(id, dto);
            if (updated == null)
                return NotFound();

            return Ok(updated);
        }

        /// <summary>
        /// Exclui um registro.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _checkRecordService.DeleteAsync(id);
            if (!deleted)
                return NotFound();

            return NoContent();
        }

        /// <summary>
        /// Realiza o check-in de um novo registro.
        /// </summary>
        [HttpPost("check-in")]
        public async Task<IActionResult> PerformCheckIn([FromBody] CreateCheckRecordDTO dto)
        {
            var result = await _checkRecordService.PerformCheckInAsync(dto);
            return Ok(result);
        }

        /// <summary>
        /// Realiza o check-out de um registro existente.
        /// </summary>
        [HttpPost("check-out/{id}")]
        public async Task<IActionResult> PerformCheckOut(int id)
        {
            var result = await _checkRecordService.PerformCheckOutAsync(id);
            if (result == null)
                return BadRequest("Registro não encontrado ou status inválido para check-out.");

            return Ok(result);
        }
    }
}
