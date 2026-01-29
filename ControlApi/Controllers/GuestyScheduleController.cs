using Core.DTO.Guesty;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Services.Integrations.Guesty;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using Services.Security;

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
        private readonly IMemoryCache _cache;
        private readonly ICurrentUser _currentUser;

        public GuestyScheduleController(
            IGuestyScheduleService schedule,
            IGuestyIntegrationService integration,
            IGuestyOpenApiClient client,
            IMemoryCache cache,
            ICurrentUser currentUser)
        {
            _schedule = schedule;
            _integration = integration;
            _client = client;
            _cache = cache;
            _currentUser = currentUser;
        }

        // GET /api/GuestySchedule/listings?limit=100&skip=0
        [HttpGet("listings")]
        public async Task<ActionResult<List<GuestyListingDTO>>> GetListings(
            [FromQuery] int limit = 100,
            [FromQuery] int skip = 0,
            [FromQuery] string? city = null,
            [FromQuery] string? status = null)
        {
            // Booking Engine API doesn't support `skip` (cursor-based pagination). We keep `skip` for backwards compatibility,
            // but it is ignored.

            var companyId = _currentUser.CompanyId;
            var safeLimit = Math.Clamp(limit, 1, 100);
            var key = companyId.HasValue
                ? $"guesty:listings:{companyId.Value}:{safeLimit}:{city ?? ""}:{status ?? ""}"
                : null;

            if (key != null && _cache.TryGetValue(key, out List<GuestyListingDTO> cached))
                return Ok(cached);

            var token = await _integration.GetAccessTokenOrThrowAsync();
            var listings = await _client.GetListingsAsync(token, safeLimit, null, city, status);

            if (key != null)
                _cache.Set(key, listings, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                });

            return Ok(listings);
        }

        // GET /api/GuestySchedule?startDate=YYYY-MM-DD&endDate=YYYY-MM-DD&listingIds=ID1&listingIds=ID2
        [HttpGet]
        public async Task<ActionResult<GuestyScheduleResponse>> GetSchedule(
            [FromQuery] string startDate,
            [FromQuery] string endDate,
            [FromQuery] List<string>? listingIds = null)
        {
            var companyId = _currentUser.CompanyId;
            var listKey = listingIds == null || listingIds.Count == 0
                ? "all"
                : string.Join(",", listingIds.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).OrderBy(x => x));

            var key = companyId.HasValue
                ? $"guesty:schedule:{companyId.Value}:{startDate}:{endDate}:{listKey}"
                : null;

            if (key != null && _cache.TryGetValue(key, out GuestyScheduleResponse cached))
                return Ok(cached);

            var response = await _schedule.GetScheduleAsync(startDate, endDate, listingIds);

            if (key != null)
                _cache.Set(key, response, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(45)
                });

            return Ok(response);
        }
    }
}
