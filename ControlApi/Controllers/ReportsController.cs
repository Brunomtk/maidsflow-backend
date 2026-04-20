using System.Threading.Tasks;
using Core.DTO.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace ControlApi.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/reports")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportsService _reportsService;
        private readonly Services.Email.ICompanyReportEmailService _companyReportEmailService;

        public ReportsController(IReportsService reportsService, Services.Email.ICompanyReportEmailService companyReportEmailService)
        {
            _reportsService = reportsService;
            _companyReportEmailService = companyReportEmailService;
        }

        [HttpGet("company")]
        public async Task<ActionResult> GetCompanyReport([FromQuery] ReportQueryDto query)
        {
            var result = await _reportsService.GetCompanyReportAsync(query);
            return Ok(result);
        }

        [HttpGet("company/export/csv")]
        public async Task<IActionResult> ExportCompanyReportCsv([FromQuery] ReportQueryDto query)
        {
            var bytes = await _reportsService.ExportCompanyReportCsvAsync(query);
            return File(bytes, "text/csv; charset=utf-8", "company-report.csv");
        }

        [HttpPost("company/email/send")]
        public async Task<ActionResult> SendCompanyReportEmail([FromBody] SendCompanyReportEmailRequestDto request)
        {
            var companyId = request.CompanyId ?? 0;
            if (companyId <= 0 && int.TryParse(User.FindFirst("companyId")?.Value, out var claimCompanyId))
                companyId = claimCompanyId;

            var result = await _companyReportEmailService.SendAsync(companyId, request, "manual");
            return Ok(result);
        }

        [HttpGet("admin")]
        public async Task<ActionResult> GetAdminReport([FromQuery] ReportQueryDto query)
        {
            var result = await _reportsService.GetAdminReportAsync(query);
            return Ok(result);
        }

        [HttpGet("admin/export/csv")]
        public async Task<IActionResult> ExportAdminReportCsv([FromQuery] ReportQueryDto query)
        {
            var bytes = await _reportsService.ExportAdminReportCsvAsync(query);
            return File(bytes, "text/csv; charset=utf-8", "admin-report.csv");
        }
    }
}
