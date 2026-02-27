// Core/DTO/GpsTracking/CreateGpsTrackingDTO.cs
using System;
using Core.Enums.GpsTracking;

namespace Core.DTO.GpsTracking
{
    public class CreateGpsTrackingDTO
    {
        public int ProfessionalId { get; set; }
        public string? ProfessionalName { get; set; }

        public int CompanyId { get; set; }
        public string? CompanyName { get; set; }

        public int? TeamId { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Address { get; set; }
        public GpsTrackingStatus? Status { get; set; }

        public GpsTrackingSource? Source { get; set; }
        public int? AppointmentId { get; set; }
        public int? CustomerId { get; set; }
        public int? CheckRecordId { get; set; }

        public string? Notes { get; set; }

        public DateTime? Timestamp { get; set; }
    }
}
