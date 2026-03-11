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

        public ReportsController(IReportsService reportsService)
        {
            _reportsService = reportsService;
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
