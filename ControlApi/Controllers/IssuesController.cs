using Core.DTO.Issues;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using Services.Storage;
using System.Linq;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class IssuesController : ControllerBase
    {
        private readonly IServiceIssueService _issueService;
        private readonly IS3StorageService _s3;

        public IssuesController(IServiceIssueService issueService, IS3StorageService s3)
        {
            _issueService = issueService;
            _s3 = s3;
        }

        [HttpGet]
        public async Task<IActionResult> GetCompanyIssues()
        {
            var issues = await _issueService.GetByCompanyAsync();
            return Ok(issues.Select(ToIssueResponse));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var issue = await _issueService.GetByIdAsync(id);
            return issue != null ? Ok(ToIssueResponse(issue)) : NotFound();
        }

        [HttpGet("appointment/{appointmentId}")]
        public async Task<IActionResult> GetByAppointment(int appointmentId)
        {
            var issues = await _issueService.GetByAppointmentAsync(appointmentId);
            return Ok(issues.Select(ToIssueResponse));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateServiceIssueDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var created = await _issueService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, ToIssueResponse(created));
        }

        [HttpPost("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateServiceIssueStatusDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var updated = await _issueService.UpdateStatusAsync(id, dto);
            return updated != null ? Ok(ToIssueResponse(updated)) : NotFound();
        }

        [HttpPost("appointment/{appointmentId}/photos/presign")]
        public async Task<IActionResult> PresignPhotoUpload(int appointmentId, [FromBody] PresignIssuePhotoUploadRequest request)
        {
            _ = await _issueService.GetByAppointmentAsync(appointmentId);
            // access checked inside service call above
            var fileName = string.IsNullOrWhiteSpace(request.FileName) ? "issue-photo.jpg" : request.FileName.Trim();
            var contentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType.Trim();
            var presign = _s3.CreateIssuePhotoUploadUrl(appointmentId, request.IssueId ?? 0, fileName, contentType);

            return Ok(new PresignIssuePhotoUploadResponse
            {
                Key = presign.Key,
                UploadUrl = presign.UploadUrl,
                DownloadUrl = _s3.CreateDownloadUrl(presign.Key),
                ExpiresAtUtc = presign.ExpiresAtUtc.UtcDateTime
            });
        }

        private object ToIssueResponse(Core.Models.ServiceIssue issue)
        {
            var photoKeys = (issue.PhotoUrls ?? new System.Collections.Generic.List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => _s3.TryGetKeyFromStoredValue(x, out var key) ? key : x)
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new
            {
                issue.Id,
                issue.CompanyId,
                issue.AppointmentId,
                issue.CustomerId,
                issue.CustomerAddressId,
                issue.ProfessionalId,
                issue.ReportedByUserId,
                issue.ReviewedByUserId,
                issue.Type,
                issue.Status,
                issue.Summary,
                issue.Description,
                issue.InternalNotes,
                issue.EstimatedAmount,
                issue.ApprovedAmount,
                PhotoKeys = photoKeys,
                PhotoUrls = photoKeys.Select(x => _s3.CreateDownloadUrl(x) ?? x).ToList(),
                issue.ResolvedAtUtc,
                issue.CreatedDate,
                issue.UpdatedDate,
                CustomerName = issue.Customer?.Name,
                CustomerAddressLabel = issue.CustomerAddress?.Label,
                ProfessionalName = issue.Professional?.Name,
                AppointmentStart = issue.Appointment?.Start,
                AppointmentEnd = issue.Appointment?.End
            };
        }

    }
}
