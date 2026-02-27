// Core/DTO/GpsTracking/GpsTrackingDTO.cs
using System;
using Core.Enums.GpsTracking;

namespace Core.DTO.GpsTracking
{
    public class GpsTrackingDTO
    {
        public int Id { get; set; }

        public int ProfessionalId { get; set; }
        public string? ProfessionalName { get; set; }

        public int CompanyId { get; set; }
        public string? CompanyName { get; set; }

        public int? TeamId { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Address { get; set; } = string.Empty;
        public GpsTrackingStatus Status { get; set; }
        public string? Notes { get; set; }

        public DateTime Timestamp { get; set; }

        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
    }
}
