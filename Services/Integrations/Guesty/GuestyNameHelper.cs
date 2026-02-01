using System;
using Core.DTO.Guesty;

namespace Services.Integrations.Guesty
{
    /// <summary>
    /// Single source of truth for how we display Guesty listing names.
    /// We use the same rule for:
    /// - /GuestySchedule/listings
    /// - schedule events (calendar)
    /// - CustomerAddresses sync (GuestyListingTitle + Label)
    /// This avoids "name mismatch" bugs when the frontend tries to match data.
    /// </summary>
    public static class GuestyNameHelper
    {
        public static string GetListingDisplayName(GuestyListingDTO listing)
        {
            if (listing == null) return "Guesty";
            var name = (listing.Nickname ?? listing.Title ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(name)) return name;
            return string.IsNullOrWhiteSpace(listing.Id) ? "Guesty" : $"Guesty {listing.Id}";
        }
    }
}
