using System;
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
        [HttpGet("public-config")]
        [AllowAnonymous]
        public IActionResult GetPublicConfig()
        {
            var key = _config["WebPush:PublicKey"] ?? string.Empty;
            var configured = !string.IsNullOrWhiteSpace(key) && !key.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase);

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
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var saved = await _service.UpsertAsync(userId, dto);
            return Ok(saved);
        }

        // DELETE: api/PushSubscriptions/unsubscribe
        [HttpDelete("unsubscribe")]
        public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeDTO dto)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var ok = await _service.UnsubscribeAsync(userId, dto.Endpoint);
            if (!ok) return NotFound();
            return NoContent();
        }

        // GET: api/PushSubscriptions/me
        [HttpGet("me")]
        public async Task<IActionResult> GetMine()
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var list = await _service.GetMySubscriptionsAsync(userId);
            return Ok(list);
        }

        // POST: api/PushSubscriptions/test
        [HttpPost("test")]
        public async Task<IActionResult> SendTest([FromBody] PushNotificationTestDTO dto)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var result = await _service.SendTestAsync(userId, dto);
            if (result == null) return NotFound(new { message = "Nenhuma subscription ativa encontrada para enviar o push de teste." });
            return Ok(result);
        }

        // POST: api/PushSubscriptions/opened
        [HttpPost("opened")]
        public async Task<IActionResult> MarkOpened([FromBody] PushNotificationOpenedDTO dto)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var ok = await _service.MarkOpenedAsync(userId, dto);
            if (!ok) return NotFound();
            return NoContent();
        }

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return !string.IsNullOrWhiteSpace(userIdStr) && int.TryParse(userIdStr, out userId);
        }
    }
}
