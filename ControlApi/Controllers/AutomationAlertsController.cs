using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.DTO.AutomationAlerts;
using Core.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Services.AutomationAlerts;
using Services.Security;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/automation-alerts")]
    public class AutomationAlertsController : ControllerBase
    {
        private readonly IAutomationFailureAlertService _service;
        private readonly ICurrentUser _currentUser;
        private readonly IOptions<AutomationAlertsOptions> _options;

        public AutomationAlertsController(
            IAutomationFailureAlertService service,
            ICurrentUser currentUser,
            IOptions<AutomationAlertsOptions> options)
        {
            _service = service;
            _currentUser = currentUser;
            _options = options;
        }

        [HttpPost("workflow-failures")]
        [AllowAnonymous]
        public async Task<ActionResult<AutomationFailureLogDto>> CreateWorkflowFailure(
            [FromBody] CreateAutomationFailureAlertRequest request,
            [FromHeader(Name = "X-Automation-Alert-Secret")] string? secretHeader,
            CancellationToken ct)
        {
            var expectedSecret = _options.Value.WebhookSecret?.Trim();
            var providedSecret = string.IsNullOrWhiteSpace(secretHeader) ? request.Secret?.Trim() : secretHeader.Trim();
            if (string.IsNullOrWhiteSpace(expectedSecret) || expectedSecret != providedSecret)
                return Unauthorized();

            var result = await _service.RecordAndNotifyAsync(request, ct);
            return Ok(result);
        }

        [HttpGet("workflow-failures")]
        [Authorize]
        public async Task<ActionResult<IReadOnlyList<AutomationFailureLogDto>>> GetWorkflowFailures(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            if (!_currentUser.IsAdmin && !_currentUser.CompanyId.HasValue)
                return Forbid();

            var result = await _service.GetRecentAsync(page, pageSize, ct);
            return Ok(result);
        }
    }
}
