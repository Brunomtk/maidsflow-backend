using System;
using Core.Enums.GpsTracking;

namespace Core.Models
{
    public class GpsTracking : BaseModel
    {
        // Changed to int to align with DTOs and filters
        public int ProfessionalId { get; set; }
        public string? ProfessionalName { get; set; }

        public int CompanyId { get; set; }
        public string? CompanyName { get; set; }

        public int? TeamId { get; set; }

        // Location holds latitude, longitude and address
        public Location Location { get; set; } = new Location();

        public GpsTrackingStatus Status { get; set; }

        public GpsTrackingSource Source { get; set; } = GpsTrackingSource.Gps;

        public int? AppointmentId { get; set; }
        public int? CustomerId { get; set; }
        public int? CheckRecordId { get; set; }

        public string? Notes { get; set; }

        public DateTime Timestamp { get; set; }
    }
}
