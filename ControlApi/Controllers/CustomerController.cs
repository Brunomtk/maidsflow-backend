using ClosedXML.Excel;
using Core.DTO.Customer;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using Services.Storage;
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
        private readonly IS3StorageService _s3;

        public CustomerController(ICustomerService customerService, ICustomerAddressService customerAddressService, IAppointmentService appointmentService, IPaymentService paymentService, IS3StorageService s3)
        {
            _customerService = customerService;
            _customerAddressService = customerAddressService;
            _appointmentService = appointmentService;
            _paymentService = paymentService;
            _s3 = s3;
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
            return updated != null ? Ok(ToHouseNotesResponse(updated, _s3)) : NotFound();
        }

        [HttpGet("{id}/addresses/{addressId}/house-notes")]
        public async Task<IActionResult> GetHouseNotes(int id, int addressId)
        {
            if (id <= 0 || addressId <= 0) return BadRequest("Invalid id.");

            var address = await _customerAddressService.GetByIdForCustomerAsync(id, addressId);
            if (address == null) return NotFound();

            return Ok(ToHouseNotesResponse(address, _s3));
        }

        [HttpPut("{id}/addresses/{addressId}/house-notes")]
        public async Task<IActionResult> UpdateHouseNotes(int id, int addressId, [FromBody] UpdateCustomerAddressDTO dto)
        {
            if (id <= 0 || addressId <= 0) return BadRequest("Invalid id.");
            var updated = await _customerAddressService.UpdateAsync(id, addressId, dto);
            return updated != null ? Ok(ToHouseNotesResponse(updated, _s3)) : NotFound();
        }


        [HttpPost("{id}/addresses/{addressId}/house-notes/photos/presign")]
        public async Task<IActionResult> PresignHouseNotesPhotoUpload(int id, int addressId, [FromBody] PresignHouseNotesPhotoUploadRequest request)
        {
            if (id <= 0 || addressId <= 0) return BadRequest("Invalid id.");

            var address = await _customerAddressService.GetByIdForCustomerAsync(id, addressId);
            if (address == null) return NotFound();

            var fileName = string.IsNullOrWhiteSpace(request.FileName) ? "house-note-photo.jpg" : request.FileName.Trim();
            var contentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType.Trim();
            var presign = _s3.CreateHouseNotesPhotoUploadUrl(id, addressId, fileName, contentType);

            return Ok(new PresignHouseNotesPhotoUploadResponse
            {
                Key = presign.Key,
                UploadUrl = presign.UploadUrl,
                DownloadUrl = _s3.CreateDownloadUrl(presign.Key),
                ExpiresAtUtc = presign.ExpiresAtUtc.UtcDateTime
            });
        }

        private static HouseNotesResponseDTO ToHouseNotesResponse(CustomerAddress address, IS3StorageService s3)
        {
            var photoKeys = (address.HousePhotoUrls ?? new System.Collections.Generic.List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => s3.TryGetKeyFromStoredValue(x, out var key) ? key : x)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new HouseNotesResponseDTO
            {
                AddressId = address.Id,
                CustomerId = address.CustomerId,
                AccessNotes = address.HouseAccessNotes,
                GateCode = address.HouseGateCode,
                HasPets = address.HouseHasPets,
                PetNotes = address.HousePetNotes,
                RestrictionsNotes = address.HouseRestrictionsNotes,
                PriorityNotes = address.HousePriorityNotes,
                PhotoKeys = photoKeys,
                PhotoUrls = photoKeys.Select(x => s3.CreateDownloadUrl(x) ?? x).ToList()
            };
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
                "phone2",
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
            ws.Cell(2, 4).Value = "+1 (555) 987-6543";
            ws.Cell(2, 5).Value = "123 Main St, Apt 4";
            ws.Cell(2, 6).Value = "90210";
            ws.Cell(2, 7).Value = "Beverly Hills";
            ws.Cell(2, 8).Value = "CA";
            ws.Cell(2, 9).Value = "VIP client";
            ws.Cell(2, 10).Value = "123456789"; // SSN digits only
            ws.Cell(2, 11).Value = true;
            ws.Cell(2, 12).Value = true;
            ws.Cell(2, 13).Value = 150;
            ws.Cell(2, 14).Value = "weekly";
            ws.Cell(2, 15).Value = "cash";

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

        private static string? Trimmed(string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>
        /// Cria um novo cliente.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCustomerDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var customer = new Customer
            {
                Name = (dto.Name ?? string.Empty).Trim(),
                Ssn = Trimmed(dto.Ssn),
                Email = Trimmed(dto.Email),
                Phone = Trimmed(dto.Phone) ?? string.Empty,
                Phone2 = Trimmed(dto.Phone2),
                Address = (dto.Address ?? string.Empty).Trim(),
                ZipCode = Trimmed(dto.ZipCode),
                City = Trimmed(dto.City) ?? string.Empty,
                State = Trimmed(dto.State) ?? string.Empty,
                Observations = Trimmed(dto.Observations),
                Ticket = dto.Ticket,
                Frequency = Trimmed(dto.Frequency),
                PaymentMethod = Trimmed(dto.PaymentMethod),
                ReceiveSms = dto.ReceiveSms,
                ReceiveEmail = dto.ReceiveEmail,
                Language = Trimmed(dto.Language),
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

            // Trim and treat empty-string as null so we can distinguish "user didn't send" from "user cleared the field".
            // Empty string from frontend → null on the entity (cleared field).
            // null from frontend → keep existing.
            if (dto.Name != null) existing.Name = dto.Name.Trim();
            if (dto.Ssn != null) existing.Ssn = Trimmed(dto.Ssn);
            if (dto.Email != null) existing.Email = Trimmed(dto.Email);
            if (dto.Phone != null) existing.Phone = Trimmed(dto.Phone) ?? string.Empty;
            if (dto.Phone2 != null) existing.Phone2 = Trimmed(dto.Phone2);

            if (dto.ReceiveSms.HasValue) existing.ReceiveSms = dto.ReceiveSms.Value;
            if (dto.ReceiveEmail.HasValue) existing.ReceiveEmail = dto.ReceiveEmail.Value;
            if (dto.Language != null)
            {
                var lang = Trimmed(dto.Language);
                if (lang != null) existing.Language = lang; // never null-out language
            }
            if (dto.Address != null) existing.Address = dto.Address.Trim();
            if (dto.ZipCode != null) existing.ZipCode = Trimmed(dto.ZipCode);
            if (dto.City != null) existing.City = Trimmed(dto.City) ?? string.Empty;
            if (dto.State != null) existing.State = Trimmed(dto.State) ?? string.Empty;
            if (dto.Observations != null) existing.Observations = Trimmed(dto.Observations);

            if (dto.Ticket.HasValue) existing.Ticket = dto.Ticket.Value;
            if (dto.Frequency != null) existing.Frequency = Trimmed(dto.Frequency);
            if (dto.PaymentMethod != null) existing.PaymentMethod = Trimmed(dto.PaymentMethod);

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
