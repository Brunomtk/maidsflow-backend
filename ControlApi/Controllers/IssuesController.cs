using Core.DTO.Issues;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class IssuesController : ControllerBase
    {
        private readonly IServiceIssueService _issueService;

        public IssuesController(IServiceIssueService issueService)
        {
            _issueService = issueService;
        }

        [HttpGet]
        public async Task<IActionResult> GetCompanyIssues()
        {
            var issues = await _issueService.GetByCompanyAsync();
            return Ok(issues);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var issue = await _issueService.GetByIdAsync(id);
            return issue != null ? Ok(issue) : NotFound();
        }

        [HttpGet("appointment/{appointmentId}")]
        public async Task<IActionResult> GetByAppointment(int appointmentId)
        {
            var issues = await _issueService.GetByAppointmentAsync(appointmentId);
            return Ok(issues);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateServiceIssueDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var created = await _issueService.CreateAsync(dto);
            return Ok(created);
        }

        [HttpPost("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateServiceIssueStatusDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var updated = await _issueService.UpdateStatusAsync(id, dto);
            return updated != null ? Ok(updated) : NotFound();
        }
    }
}
