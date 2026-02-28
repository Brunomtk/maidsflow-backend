using System;

namespace Core.DTO.RoutePlanning
{
    public class RoutePlanRequestDTO
    {
        /// <summary>
        /// Local date in yyyy-MM-dd.
        /// </summary>
        public string Date { get; set; } = string.Empty;

        /// <summary>
        /// IANA timezone id (ex: America/Los_Angeles).
        /// Used to interpret the provided Date.
        /// </summary>
        public string TimeZoneId { get; set; } = "America/Los_Angeles";

        /// <summary>
        /// Optional route start address. If not provided, uses first appointment address of the day.
        /// </summary>
        public string? StartAddress { get; set; }

        /// <summary>
        /// Optional route end address. If not provided, uses last appointment address of the day.
        /// </summary>
        public string? EndAddress { get; set; }

        /// <summary>
        /// Travel mode: driving, walking, bicycling, transit.
        /// Default: driving.
        /// </summary>
        public string Mode { get; set; } = "driving";
    }
}
