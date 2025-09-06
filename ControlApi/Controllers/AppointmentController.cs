using Core.DTO.Appointment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AppointmentController : ControllerBase
    {
        private readonly IAppointmentService _appointmentService;

        public AppointmentController(IAppointmentService appointmentService)
        {
            _appointmentService = appointmentService;
        }

        /// <summary>
        /// Lista agendamentos com paginação e filtros diversos (companyId, customerId, status, etc).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPaged([FromQuery] AppointmentFiltersDTO filters)
        {
            var result = await _appointmentService.GetPagedAppointments(filters);
            return Ok(result);
        }

        /// <summary>
        /// Retorna um agendamento por ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var appointment = await _appointmentService.GetById(id);
            if (appointment == null)
                return NotFound();

            return Ok(appointment);
        }

        /// <summary>
        /// Cria um novo agendamento.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAppointmentDTO dto)
        {
            var success = await _appointmentService.Create(dto);
            if (!success)
                return BadRequest("Erro ao criar agendamento.");

            return Ok("Agendamento criado com sucesso.");
        }

        /// <summary>
        /// Atualiza um agendamento existente.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateAppointmentDTO dto)
        {
            var success = await _appointmentService.Update(id, dto);
            if (!success)
                return NotFound("Agendamento não encontrado ou erro ao atualizar.");

            return Ok("Agendamento atualizado com sucesso.");
        }

        /// <summary>
        /// Exclui um agendamento.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _appointmentService.Delete(id);
            if (!success)
                return NotFound("Agendamento não encontrado ou erro ao excluir.");

            return NoContent();
        }
    }
}
