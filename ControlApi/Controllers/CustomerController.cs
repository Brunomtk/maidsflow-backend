using Core.DTO.Customer;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerService _customerService;
        private readonly IAppointmentService _appointmentService;
        private readonly IPaymentService _paymentService;

        public CustomerController(ICustomerService customerService, IAppointmentService appointmentService, IPaymentService paymentService)
        {
            _customerService = customerService;
            _appointmentService = appointmentService;
            _paymentService = paymentService;
        }

        /// <summary>
        /// Retorna clientes com paginação e filtros.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPaged([FromQuery] CustomerFiltersDTO filters)
        {
            var result = await _customerService.GetPagedAsync(filters);
            return Ok(result);
        }

        /// <summary>
        /// Retorna um cliente por ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id) // ✅ Ajustado de Guid para int
        {
            if (id <= 0) return BadRequest("ID inválido.");

            var customer = await _customerService.GetByIdAsync(id);
            return customer != null ? Ok(customer) : NotFound();
        }

        /// <summary>
        /// Lista todos os agendamentos (Appointments) vinculados a um cliente.
        /// Dica: se você preferir paginação/filtros avançados, também dá pra usar GET /api/Appointment?CustomerId=...
        /// </summary>
        [HttpGet("{id}/appointments")]
        public async Task<IActionResult> GetAppointmentsByCustomer(int id, [FromQuery] DateTime? start = null, [FromQuery] DateTime? end = null)
        {
            if (id <= 0) return BadRequest("ID inválido.");

            var customer = await _customerService.GetByIdAsync(id);
            if (customer == null) return NotFound();

            var appointments = await _appointmentService.GetByCustomer(id);

            if (start.HasValue)
                appointments = appointments.Where(a => a.Start >= start.Value).ToList();
            if (end.HasValue)
                appointments = appointments.Where(a => a.End <= end.Value).ToList();

            return Ok(appointments);
        }

        /// <summary>
        /// Lista todos os pagamentos (Payments) vinculados a um cliente.
        /// Dica: também dá pra usar GET /api/Payments?CustomerId=...
        /// </summary>
        [HttpGet("{id}/payments")]
        public async Task<IActionResult> GetPaymentsByCustomer(int id, [FromQuery] DateTime? start = null, [FromQuery] DateTime? end = null)
        {
            if (id <= 0) return BadRequest("ID inválido.");

            var customer = await _customerService.GetByIdAsync(id);
            if (customer == null) return NotFound();

            var payments = await _paymentService.GetByCustomer(id);

            if (start.HasValue)
                payments = payments.Where(p => p.DueDate >= start.Value).ToList();
            if (end.HasValue)
                payments = payments.Where(p => p.DueDate <= end.Value).ToList();

            return Ok(payments);
        }

        /// <summary>
        /// Cria um novo cliente.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCustomerDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var customer = new Customer
            {
                Name = dto.Name,
                Ssn = dto.Ssn,
                Email = dto.Email,
                Phone = dto.Phone,
                Address = dto.Address,
                ZipCode = dto.ZipCode,
                City = dto.City,
                State = dto.State,
                Observations = dto.Observations,
                Ticket = dto.Ticket,
                ServiceTypeId = (dto.ServiceTypeId.HasValue && dto.ServiceTypeId.Value > 0) ? dto.ServiceTypeId.Value : null,
                Frequency = dto.Frequency,
                PaymentMethod = dto.PaymentMethod,
                ReceiveSms = dto.ReceiveSms,
                ReceiveEmail = dto.ReceiveEmail,
                CompanyId = dto.CompanyId
            };

            var created = await _customerService.CreateAsync(customer);
            return created != null ? Ok(created) : BadRequest("Erro ao criar cliente.");
        
        }

        /// <summary>
        /// Atualiza um cliente existente.
        /// </summary>
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateCustomerDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var existing = await _customerService.GetByIdAsync(dto.Id);
            if (existing == null) return NotFound();

            existing.Name = dto.Name ?? existing.Name;
            existing.Ssn = dto.Ssn ?? existing.Ssn;
            existing.Email = dto.Email ?? existing.Email;
            existing.Phone = dto.Phone ?? existing.Phone;

            if (dto.ReceiveSms.HasValue) existing.ReceiveSms = dto.ReceiveSms.Value;
            if (dto.ReceiveEmail.HasValue) existing.ReceiveEmail = dto.ReceiveEmail.Value;
            existing.Address = dto.Address ?? existing.Address;
            existing.ZipCode = dto.ZipCode ?? existing.ZipCode;
            existing.City = dto.City ?? existing.City;
            existing.State = dto.State ?? existing.State;
            existing.Observations = dto.Observations ?? existing.Observations;

            existing.Ticket = dto.Ticket ?? existing.Ticket;
            if (dto.ServiceTypeId.HasValue)
            {
                // Convention: 0 clears the value
                existing.ServiceTypeId = dto.ServiceTypeId.Value > 0 ? dto.ServiceTypeId.Value : null;
            }
            existing.Frequency = dto.Frequency ?? existing.Frequency;
            existing.PaymentMethod = dto.PaymentMethod ?? existing.PaymentMethod;

            if (dto.Status.HasValue)
                existing.Status = dto.Status.Value;

            var success = await _customerService.UpdateAsync(existing);
            return success ? Ok(existing) : StatusCode(500, "Falha ao atualizar o cliente.");
        
        }

        /// <summary>
        /// Exclui um cliente por ID.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id) // ✅ Ajustado de Guid para int
        {
            if (id <= 0) return BadRequest("ID inválido.");

            var success = await _customerService.DeleteAsync(id);
            return success ? Ok() : NotFound();
        }
    }
}