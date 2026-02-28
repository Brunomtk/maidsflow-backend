using System.Threading;
using System.Threading.Tasks;
using Core.DTO.RoutePlanning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace ControlApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RoutePlanningController : ControllerBase
    {
        private readonly IRoutePlanningService _service;

        public RoutePlanningController(IRoutePlanningService service)
        {
            _service = service;
        }

        /// <summary>
        /// Builds an optimized route for a professional for a given local day.
        /// Returns ordered stops and overview polyline.
        /// </summary>
        [HttpGet("professional/{professionalId}/day")]
        public async Task<IActionResult> GetOptimizedDayRoute(
            int professionalId,
            [FromQuery] string date,
            [FromQuery] string? timeZoneId,
            [FromQuery] string? startAddress,
            [FromQuery] string? endAddress,
            [FromQuery] string? mode,
            CancellationToken ct)
        {
            var req = new RoutePlanRequestDTO
            {
                Date = date,
                TimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? "America/Los_Angeles" : timeZoneId!,
                StartAddress = startAddress,
                EndAddress = endAddress,
                Mode = string.IsNullOrWhiteSpace(mode) ? "driving" : mode!
            };

            var result = await _service.BuildOptimizedDayRouteAsync(professionalId, req, ct);
            return Ok(result);
        }
    }
}
