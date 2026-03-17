using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using Services.Security;

namespace ControlApi.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/admin/background-jobs")]
    public class AdminBackgroundJobsController : ControllerBase
    {
        private readonly IBackgroundJobMonitorService _monitor;
        private readonly ICurrentUser _currentUser;

        public AdminBackgroundJobsController(IBackgroundJobMonitorService monitor, ICurrentUser currentUser)
        {
            _monitor = monitor;
            _currentUser = currentUser;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            if (!_currentUser.IsAdmin)
                return Forbid();

            var result = await _monitor.GetStatusesAsync(ct);
            return Ok(result);
        }

        [HttpGet("{jobKey}")]
        public async Task<IActionResult> GetByKey(string jobKey, CancellationToken ct)
        {
            if (!_currentUser.IsAdmin)
                return Forbid();

            var result = await _monitor.GetStatusByKeyAsync(jobKey, ct);
            if (result == null)
                return NotFound();

            return Ok(result);
        }

        [HttpGet("{jobKey}/executions")]
        public async Task<IActionResult> GetExecutions(string jobKey, [FromQuery] int page = 1, [FromQuery] int pageSize = 3, CancellationToken ct = default)
        {
            if (!_currentUser.IsAdmin)
                return Forbid();

            var result = await _monitor.GetExecutionsAsync(jobKey, page, pageSize, ct);
            return Ok(result);
        }
    }
}
