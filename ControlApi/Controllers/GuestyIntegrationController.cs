using Core.DTO.Guesty;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Integrations.Guesty;
using System.Threading.Tasks;

namespace ControlApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Integrations/guesty")]
    public class GuestyIntegrationController : ControllerBase
    {
        private readonly IGuestyIntegrationService _service;
        private readonly IGuestyScheduleService _schedule;

        public GuestyIntegrationController(IGuestyIntegrationService service, IGuestyScheduleService schedule)
        {
            _service = service;
            _schedule = schedule;
        }

        // GET /api/Integrations/guesty
        [HttpGet]
        public async Task<ActionResult<GuestyIntegrationStatusDTO>> GetStatus()
        {
            var status = await _service.GetStatusAsync();
            return Ok(status);
        }

        // PUT /api/Integrations/guesty
        [HttpPut]
        public async Task<ActionResult<GuestyIntegrationStatusDTO>> UpdateToken([FromBody] UpdateGuestyTokenRequest request)
        {
            var updated = await _service.UpdateTokenAsync(request);

            // Warm cache in the background so the first Schedule view is snappy.
            _ = Task.Run(async () =>
            {
                try { await _schedule.WarmupAsync(30); } catch { /* best-effort */ }
            });

            return Ok(updated);
        }

        // DELETE /api/Integrations/guesty
        [HttpDelete]
        public async Task<IActionResult> ClearToken()
        {
            await _service.ClearTokenAsync();
            return NoContent();
        }
    }
}
