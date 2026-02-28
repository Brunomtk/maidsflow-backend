using System;
using System.Collections.Generic;

namespace Core.DTO.RoutePlanning
{
    public class RoutePlanResponseDTO
    {
        public string Date { get; set; } = string.Empty;
        public string TimeZoneId { get; set; } = string.Empty;

        public string Origin { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;

        public double TotalDistanceKm { get; set; }
        public int TotalDurationMinutes { get; set; }

        /// <summary>
        /// Encoded polyline (overview_polyline.points) from Google Directions.
        /// </summary>
        public string? OverviewPolyline { get; set; }

        public List<RoutePlanStopDTO> Stops { get; set; } = new();
    }

    public class RoutePlanStopDTO
    {
        public int AppointmentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }
}
