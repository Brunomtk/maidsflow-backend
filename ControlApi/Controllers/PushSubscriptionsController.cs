using System.Security.Claims;
using System.Threading.Tasks;
using Core.DTO.PushSubscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PushSubscriptionsController : ControllerBase
    {
        private readonly IPushSubscriptionService _service;

        public PushSubscriptionsController(IPushSubscriptionService service)
        {
            _service = service;
        }

        // POST: api/PushSubscriptions/subscribe
        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] BrowserPushSubscriptionDTO dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr) || !int.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var saved = await _service.UpsertAsync(userId, dto);
            return Ok(saved);
        }

        // DELETE: api/PushSubscriptions/unsubscribe
        [HttpDelete("unsubscribe")]
        public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeDTO dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr) || !int.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var ok = await _service.UnsubscribeAsync(userId, dto.Endpoint);
            if (!ok) return NotFound();
            return NoContent();
        }

        // GET: api/PushSubscriptions/me
        [HttpGet("me")]
        public async Task<IActionResult> GetMine()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr) || !int.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var list = await _service.GetMySubscriptionsAsync(userId);
            return Ok(list);
        }
    }
}
