// Core/DTO/GpsTracking/UpdateGpsTrackingDTO.cs
using System;
using Core.Enums.GpsTracking;

namespace Core.DTO.GpsTracking
{
    public class UpdateGpsTrackingDTO
    {
        public int? ProfessionalId { get; set; }
        public string? ProfessionalName { get; set; }

        public int? CompanyId { get; set; }
        public string? CompanyName { get; set; }

        public string? Vehicle { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Address { get; set; }
        public int? Accuracy { get; set; }

        public double? Speed { get; set; }
        public GpsTrackingStatus? Status { get; set; }  

        public int? Battery { get; set; }
        public string? Notes { get; set; }

        public DateTime? Timestamp { get; set; }
    }
}
