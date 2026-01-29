using System.Collections.Generic;
using System.Threading.Tasks;
using Core.DTO.Guesty;

namespace Services.Integrations.Guesty
{
    public interface IGuestyScheduleService
    {
        // `listingsLimit` is the page size used when fetching listings from Guesty.
        // Booking Engine API uses cursor pagination, so we don't expose `skip`.
        Task<GuestyScheduleResponse> GetScheduleAsync(string startDate, string endDate, IEnumerable<string>? listingIds = null, int listingsLimit = 25, string? city = null, string? status = null);

        /// <summary>
        /// Pre-aquece o cache para deixar a primeira abertura da agenda Guesty bem mais rápida.
        /// </summary>
        Task WarmupAsync(int days = 30);
    }
}
