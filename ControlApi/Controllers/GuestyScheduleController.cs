using Core.DTO.Guesty;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Integrations.Guesty;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ControlApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class GuestyScheduleController : ControllerBase
    {
        private readonly IGuestyScheduleService _schedule;
        private readonly IGuestyIntegrationService _integration;
        private readonly IGuestyOpenApiClient _client;

        public GuestyScheduleController(
            IGuestyScheduleService schedule,
            IGuestyIntegrationService integration,
            IGuestyOpenApiClient client)
        {
            _schedule = schedule;
            _integration = integration;
            _client = client;
        }

        // GET /api/GuestySchedule/listings?limit=25&skip=0
        [HttpGet("listings")]
        public async Task<ActionResult<List<GuestyListingDTO>>> GetListings(
            [FromQuery] int limit = 25,
            [FromQuery] int skip = 0,
            [FromQuery] string? city = null,
            [FromQuery] string? status = null)
        {
            var token = await _integration.GetAccessTokenOrThrowAsync();
            // Booking Engine API doesn't support `skip` (cursor-based pagination). We keep `skip` for backwards compatibility,
            // but it is ignored.
            var listings = await _client.GetListingsAsync(token, limit, null, city, status);
            return Ok(listings);
        }

        // GET /api/GuestySchedule?startDate=YYYY-MM-DD&endDate=YYYY-MM-DD&listingIds=ID1&listingIds=ID2
        [HttpGet]
        public async Task<ActionResult<GuestyScheduleResponse>> GetSchedule(
            [FromQuery] string startDate,
            [FromQuery] string endDate,
            [FromQuery] List<string>? listingIds = null)
        {
            var response = await _schedule.GetScheduleAsync(startDate, endDate, listingIds);
            return Ok(response);
        }
    }
}
