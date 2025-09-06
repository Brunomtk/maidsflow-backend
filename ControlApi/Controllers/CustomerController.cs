using Core.DTO.Customer;
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
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerService _customerService;

        public CustomerController(ICustomerService customerService)
        {
            _customerService = customerService;
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
                City = dto.City,
                State = dto.State,
                Observations = dto.Observations,
                Ticket = dto.Ticket,
                Frequency = dto.Frequency,
                PaymentMethod = dto.PaymentMethod,
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
            existing.Address = dto.Address ?? existing.Address;
            existing.City = dto.City ?? existing.City;
            existing.State = dto.State ?? existing.State;
            existing.Observations = dto.Observations ?? existing.Observations;

            existing.Ticket = dto.Ticket ?? existing.Ticket;
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