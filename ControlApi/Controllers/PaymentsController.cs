// ControlApi/Controllers/PaymentsController.cs
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Services;
using Core.DTO.Payments;
using Infrastructure.ServiceExtension;
using Core.Models;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentsController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        // GET: api/payments
        [HttpGet]
        public async Task<ActionResult<PagedResult<Payment>>> Get([FromQuery] PaymentFiltersDto filters)
        {
            var paged = await _paymentService.GetPagedAsync(filters);
            return Ok(paged);
        }

        // GET: api/payments/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Payment>> Get(int id)
        {
            var payment = await _paymentService.GetByIdAsync(id);
            if (payment == null) return NotFound();
            return Ok(payment);
        }

        // POST: api/payments
        [HttpPost]
        public async Task<ActionResult<Payment>> Post([FromBody] CreatePaymentDto dto)
        {
            var created = await _paymentService.CreateAsync(dto);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }

        // PUT: api/payments/5
        [HttpPut("{id}")]
        public async Task<ActionResult<Payment>> Put(int id, [FromBody] UpdatePaymentDto dto)
        {
            var updated = await _paymentService.UpdateAsync(id, dto);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        // DELETE: api/payments/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _paymentService.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }

        // POST: api/payments/5/status
        [HttpPost("{id}/status")]
        public async Task<ActionResult<Payment>> ProcessStatus(int id, [FromBody] ProcessPaymentStatusDto dto)
        {
            var processed = await _paymentService.ProcessStatusAsync(id, dto);
            return Ok(processed);
        }
    }
}
