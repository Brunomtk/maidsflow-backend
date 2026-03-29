using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using Services;
using Core.DTO.Payments;
using Infrastructure.ServiceExtension;
using Core.Models;
using Core.Enums.Payment;

namespace ControlApi.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentsController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<Payment>>> Get([FromQuery] PaymentFiltersDto filters)
        {
            var paged = await _paymentService.GetPagedAsync(filters);
            return Ok(paged);
        }

        [HttpGet("accounts-payable")]
        public async Task<ActionResult<PagedResult<Payment>>> GetAccountsPayable([FromQuery] PaymentFiltersDto filters)
        {
            filters.FinancialType = PaymentFinancialType.Expense;
            var paged = await _paymentService.GetPagedAsync(filters);
            return Ok(paged);
        }

        [HttpGet("accounts-receivable")]
        public async Task<ActionResult<PagedResult<Payment>>> GetAccountsReceivable([FromQuery] PaymentFiltersDto filters)
        {
            filters.FinancialType = PaymentFinancialType.Income;
            var paged = await _paymentService.GetPagedAsync(filters);
            return Ok(paged);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Payment>> Get(int id)
        {
            var payment = await _paymentService.GetByIdAsync(id);
            if (payment == null) return NotFound();
            return Ok(payment);
        }

        [HttpPost]
        public async Task<ActionResult<Payment>> Post([FromBody] CreatePaymentDto dto)
        {
            var created = await _paymentService.CreateAsync(dto);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }

        [HttpPost("accounts-payable")]
        public async Task<ActionResult<Payment>> PostAccountsPayable([FromBody] CreatePaymentDto dto)
        {
            dto.FinancialType = PaymentFinancialType.Expense;
            var created = await _paymentService.CreateAsync(dto);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }

        [HttpPost("accounts-receivable")]
        public async Task<ActionResult<Payment>> PostAccountsReceivable([FromBody] CreatePaymentDto dto)
        {
            dto.FinancialType = PaymentFinancialType.Income;
            var created = await _paymentService.CreateAsync(dto);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Payment>> Put(int id, [FromBody] UpdatePaymentDto dto)
        {
            var updated = await _paymentService.UpdateAsync(id, dto);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _paymentService.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }

        [HttpPost("{id}/status")]
        public async Task<ActionResult<Payment>> ProcessStatus(int id, [FromBody] ProcessPaymentStatusDto dto)
        {
            var processed = await _paymentService.ProcessStatusAsync(id, dto);
            return Ok(processed);
        }
    }
}
