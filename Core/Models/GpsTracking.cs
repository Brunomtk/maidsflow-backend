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

        public string Vehicle { get; set; } = string.Empty;

        // Location holds latitude, longitude, address and accuracy
        public Location Location { get; set; } = new Location();

        public double Speed { get; set; }
        public GpsTrackingStatus Status { get; set; }

        public int Battery { get; set; }

        public string? Notes { get; set; }

        public DateTime Timestamp { get; set; }
    }
}
