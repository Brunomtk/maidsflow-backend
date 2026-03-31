using System;
using System.Collections.Generic;
using Core.DTO.Customer;

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
        public int Sequence { get; set; }
        public int AppointmentId { get; set; }
        public int? CustomerId { get; set; }
        public int? CustomerAddressId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string? Status { get; set; }

        public bool HasHouseNotes { get; set; }
        public bool HasPets { get; set; }
        public bool HasGateCode { get; set; }
        public bool HasRestrictions { get; set; }
        public bool HasPriorityNotes { get; set; }
        public HouseNotesSnapshotDTO? HouseNotes { get; set; }

        public int TotalIssueCount { get; set; }
        public int OpenIssueCount { get; set; }
        public bool HasOpenIssues { get; set; }
    }
}
