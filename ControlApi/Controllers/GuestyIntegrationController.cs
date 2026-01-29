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

        public GuestyIntegrationController(IGuestyIntegrationService service)
        {
            _service = service;
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
