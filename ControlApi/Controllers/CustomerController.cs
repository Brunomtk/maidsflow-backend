using ClosedXML.Excel;
using Core.DTO.Customer;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using System;
using System.IO;
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
        private readonly ICustomerAddressService _customerAddressService;
        private readonly IAppointmentService _appointmentService;
        private readonly IPaymentService _paymentService;

        public CustomerController(ICustomerService customerService, ICustomerAddressService customerAddressService, IAppointmentService appointmentService, IPaymentService paymentService)
        {
            _customerService = customerService;
            _customerAddressService = customerAddressService;
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
        public async Task<IActionResult> GetById(int id)
        {
            if (id <= 0) return BadRequest("ID inválido.");

            var customer = await _customerService.GetByIdAsync(id);
            return customer != null ? Ok(customer) : NotFound();
        }

        [HttpGet("{id}/addresses")]
        public async Task<IActionResult> GetAddresses(int id)
        {
            if (id <= 0) return BadRequest("ID inválido.");
            var addresses = await _customerAddressService.GetByCustomerAsync(id);
            return Ok(addresses.OrderByDescending(a => a.IsPrimary).ThenBy(a => a.Id));
        }

        [HttpPost("{id}/addresses")]
        public async Task<IActionResult> CreateAddress(int id, [FromBody] CreateCustomerAddressDTO dto)
        {
            if (id <= 0) return BadRequest("ID inválido.");
            var created = await _customerAddressService.CreateAsync(id, dto);
            return created != null ? Ok(created) : NotFound();
        }

        [HttpPut("{id}/addresses/{addressId}")]
        public async Task<IActionResult> UpdateAddress(int id, int addressId, [FromBody] UpdateCustomerAddressDTO dto)
        {
            if (id <= 0 || addressId <= 0) return BadRequest("ID inválido.");
            var updated = await _customerAddressService.UpdateAsync(id, addressId, dto);
            return updated != null ? Ok(updated) : NotFound();
        }

        [HttpGet("{id}/addresses/{addressId}/house-notes")]
        public async Task<IActionResult> GetHouseNotes(int id, int addressId)
        {
            if (id <= 0 || addressId <= 0) return BadRequest("Invalid id.");

            var addresses = await _customerAddressService.GetByCustomerAsync(id);
            var address = addresses.FirstOrDefault(a => a.Id == addressId);
            if (address == null) return NotFound();

            return Ok(new
            {
                address.Id,
                address.CustomerId,
                address.HouseAccessNotes,
                address.HouseGateCode,
                address.HouseHasPets,
                address.HousePetNotes,
                address.HouseRestrictionsNotes,
                address.HousePriorityNotes,
                housePhotoUrls = address.HousePhotoUrls
            });
        }

        [HttpPut("{id}/addresses/{addressId}/house-notes")]
        public async Task<IActionResult> UpdateHouseNotes(int id, int addressId, [FromBody] UpdateCustomerAddressDTO dto)
        {
            if (id <= 0 || addressId <= 0) return BadRequest("Invalid id.");
            var updated = await _customerAddressService.UpdateAsync(id, addressId, dto);
            return updated != null ? Ok(updated) : NotFound();
        }

        [HttpDelete("{id}/addresses/{addressId}")]
        public async Task<IActionResult> DeleteAddress(int id, int addressId)
        {
            if (id <= 0 || addressId <= 0) return BadRequest("ID inválido.");
            var ok = await _customerAddressService.DeleteAsync(id, addressId);
            return ok ? NoContent() : NotFound();
        }

        [HttpPost("{id}/addresses/{addressId}/set-primary")]
        public async Task<IActionResult> SetPrimaryAddress(int id, int addressId)
        {
            if (id <= 0 || addressId <= 0) return BadRequest("ID inválido.");
            var ok = await _customerAddressService.SetPrimaryAsync(id, addressId);
            return ok ? Ok() : NotFound();
        }

        /// <summary>
        /// Baixa um template Excel com as colunas esperadas para cadastrar clientes.
        /// Observação: o campo 'ssn' é o documento US (Social Security Number) quando aplicável.
        /// </summary>
        [HttpGet("excel-template")]
        public IActionResult DownloadExcelTemplate()
        {
            using var wb = new XLWorkbook();

            // ---- Sheet: Clients ----
            var ws = wb.Worksheets.Add("Clients");
            var headers = new[]
            {
                "name",
                "email",
                "phone",
                "address",
                "zipCode",
                "city",
                "state",
                "observations",
                "ssn",
                "receiveSms",
                "receiveEmail",
                "ticket",
                "frequency",
                "paymentMethod"
            };

            for (var i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
            }

            // Example row (optional)
            ws.Cell(2, 1).Value = "John Doe";
            ws.Cell(2, 2).Value = "john@example.com";
            ws.Cell(2, 3).Value = "+1 (555) 123-4567";
            ws.Cell(2, 4).Value = "123 Main St, Apt 4";
            ws.Cell(2, 5).Value = "90210";
            ws.Cell(2, 6).Value = "Beverly Hills";
            ws.Cell(2, 7).Value = "CA";
            ws.Cell(2, 8).Value = "VIP client";
            ws.Cell(2, 9).Value = "123456789"; // SSN digits only
            ws.Cell(2, 10).Value = true;
            ws.Cell(2, 11).Value = true;
            ws.Cell(2, 12).Value = 150;
            ws.Cell(2, 13).Value = "weekly";
            ws.Cell(2, 14).Value = "cash";

            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();

            // ---- Sheet: Instructions ----
            var wi = wb.Worksheets.Add("Instructions");
            wi.Cell(1, 1).Value = "How to use";
            wi.Cell(1, 1).Style.Font.Bold = true;

            wi.Cell(3, 1).Value = "1) Keep the header row (row 1) exactly as provided.";
            wi.Cell(4, 1).Value = "2) Required fields: name, address.";
            wi.Cell(5, 1).Value = "3) state must be 2 letters (e.g., CA, NY).";
            wi.Cell(6, 1).Value = "4) ssn is optional; when provided, use digits only.";
            wi.Cell(7, 1).Value = "5) receiveSms/receiveEmail: leave blank to default true.";
            wi.Cell(8, 1).Value = "6) Import is done by sending rows to POST /api/Customer/bulk.";
            wi.Columns().AdjustToContents();

            // ---- Sheet: Reference ----
            var wr = wb.Worksheets.Add("Reference");
            wr.Cell(1, 1).Value = "Field";
            wr.Cell(1, 2).Value = "Notes";
            wr.Row(1).Style.Font.Bold = true;

            wr.Cell(2, 1).Value = "state";
            wr.Cell(2, 2).Value = "US state abbreviation (2 letters).";
            wr.Cell(3, 1).Value = "ssn";
            wr.Cell(3, 2).Value = "US Social Security Number (digits only).";
            wr.Cell(4, 1).Value = "zipCode";
            wr.Cell(4, 2).Value = "ZIP (12345) or ZIP+4 (12345-6789).";
            wr.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var bytes = ms.ToArray();

            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "clients_template.xlsx"
            );
        }

        /// <summary>
        /// Importação em lote (bulk) de clientes. O CompanyId é inferido pelo escopo para usuários Company.
        /// Para Admin, informe CompanyId no payload.
        /// </summary>
        [HttpPost("bulk")]
        public async Task<IActionResult> BulkCreate([FromBody] BulkCreateCustomersRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _customerService.BulkCreateAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Lista todos os agendamentos (Appointments) vinculados a um cliente.
        /// Dica: se você preferir paginação/filtros avançados, também dá pra usar GET /api/Appointment?CustomerId=...
        /// </summary>
        [HttpGet("{id}/appointments")]
        public async Task<IActionResult> GetAppointmentsByCustomer(int id, [FromQuery] int? customerAddressId = null, [FromQuery] DateTime? start = null, [FromQuery] DateTime? end = null)
        {
            if (id <= 0) return BadRequest("ID inválido.");

            var customer = await _customerService.GetByIdAsync(id);
            if (customer == null) return NotFound();

            var appointments = await _appointmentService.GetByCustomer(id);

            if (customerAddressId.HasValue)
                appointments = appointments.Where(a => a.CustomerAddressId == customerAddressId.Value).ToList();

            if (start.HasValue)
                appointments = appointments.Where(a => a.Start >= start.Value).ToList();
            if (end.HasValue)
                appointments = appointments.Where(a => a.End <= end.Value).ToList();

            return Ok(appointments);
        }

        [HttpGet("{id}/addresses/{addressId}/appointments")]
        public async Task<IActionResult> GetAppointmentsByCustomerAddress(int id, int addressId, [FromQuery] DateTime? start = null, [FromQuery] DateTime? end = null)
        {
            if (id <= 0 || addressId <= 0) return BadRequest("ID inválido.");

            var addresses = await _customerAddressService.GetByCustomerAsync(id);
            if (!addresses.Any(a => a.Id == addressId)) return NotFound();

            var appointments = await _appointmentService.GetByCustomer(id);
            appointments = appointments.Where(a => a.CustomerAddressId == addressId).ToList();

            if (start.HasValue)
                appointments = appointments.Where(a => a.Start >= start.Value).ToList();
            if (end.HasValue)
                appointments = appointments.Where(a => a.End <= end.Value).ToList();

            return Ok(appointments);
        }

        [HttpGet("{id}/addresses/{addressId}/payments")]
        public async Task<IActionResult> GetPaymentsByCustomerAddress(int id, int addressId, [FromQuery] DateTime? start = null, [FromQuery] DateTime? end = null)
        {
            if (id <= 0 || addressId <= 0) return BadRequest("ID inválido.");

            var addresses = await _customerAddressService.GetByCustomerAsync(id);
            if (!addresses.Any(a => a.Id == addressId)) return NotFound();

            var payments = await _paymentService.GetByCustomer(id);
            payments = payments.Where(p => p.CustomerAddressId == addressId).ToList();

            if (start.HasValue)
                payments = payments.Where(p => p.DueDate >= start.Value).ToList();
            if (end.HasValue)
                payments = payments.Where(p => p.DueDate <= end.Value).ToList();

            return Ok(payments);
        }

        /// <summary>
        /// Lista todos os pagamentos (Payments) vinculados a um cliente.
        /// Dica: também dá pra usar GET /api/Payments?CustomerId=...
        /// </summary>
        [HttpGet("{id}/payments")]
        public async Task<IActionResult> GetPaymentsByCustomer(int id, [FromQuery] int? customerAddressId = null, [FromQuery] DateTime? start = null, [FromQuery] DateTime? end = null)
        {
            if (id <= 0) return BadRequest("ID inválido.");

            var customer = await _customerService.GetByIdAsync(id);
            if (customer == null) return NotFound();

            var payments = await _paymentService.GetByCustomer(id);

            if (customerAddressId.HasValue)
                payments = payments.Where(p => p.CustomerAddressId == customerAddressId.Value).ToList();

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
                Phone = dto.Phone ?? string.Empty,
                Address = dto.Address,
                ZipCode = dto.ZipCode,
                City = dto.City ?? string.Empty,
                State = dto.State ?? string.Empty,
                Observations = dto.Observations,
                Ticket = dto.Ticket,
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
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0) return BadRequest("ID inválido.");

            var success = await _customerService.DeleteAsync(id);
            return success ? Ok() : NotFound();
        }
    }
}
