using System.Security.Claims;
using System.Threading.Tasks;
using Core.DTO.PushSubscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Services;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PushSubscriptionsController : ControllerBase
    {
        private readonly IPushSubscriptionService _service;
        private readonly IConfiguration _config;

        public PushSubscriptionsController(IPushSubscriptionService service, IConfiguration config)
        {
            _service = service;
            _config = config;
        }

        // GET: api/PushSubscriptions/public-config
        // Endpoint público para o frontend obter a VAPID Public Key automaticamente.
        [HttpGet("public-config")]
        [AllowAnonymous]
        public IActionResult GetPublicConfig()
        {
            var key = _config["WebPush:PublicKey"] ?? string.Empty;
            var configured = !string.IsNullOrWhiteSpace(key) && !key.StartsWith("CHANGE_ME", System.StringComparison.OrdinalIgnoreCase);

            return Ok(new PublicPushConfigDTO
            {
                VapidPublicKey = key,
                Configured = configured
            });
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
