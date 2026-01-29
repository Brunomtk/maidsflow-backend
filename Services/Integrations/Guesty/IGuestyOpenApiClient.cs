using System.Collections.Generic;
using System.Threading.Tasks;
using Core.DTO.Guesty;

namespace Services.Integrations.Guesty
{
    public interface IGuestyOpenApiClient
    {
        // Booking Engine API uses cursor-based pagination (no `skip`).
        // `limit` is the page size (we clamp it to a safe max to avoid Guesty 400).
        Task<List<GuestyListingDTO>> GetListingsAsync(string accessToken, int limit = 25, string? cursor = null, string? city = null, string? status = null);

        // Returns raw json from Guesty calendar endpoint (bulk) to keep compatibility with Guesty changes.
        Task<string> GetCalendarRawAsync(string accessToken, string startDate, string endDate, IEnumerable<string>? listingIds = null);
    }
}
