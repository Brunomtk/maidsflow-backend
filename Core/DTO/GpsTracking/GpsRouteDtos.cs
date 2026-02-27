// Core/DTO/GpsTracking/GpsRouteDtos.cs
using System;
using System.Collections.Generic;
using Core.Enums.GpsTracking;

namespace Core.DTO.GpsTracking
{
    public class GpsRoutePointDTO
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Address { get; set; }
        public DateTime TimestampUtc { get; set; }

        public GpsTrackingSource Source { get; set; } = GpsTrackingSource.Gps;
        public int? AppointmentId { get; set; }
        public int? CustomerId { get; set; }
        public int? CheckRecordId { get; set; }
    }

    public class GpsRouteStopDTO
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Address { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public double DurationMinutes { get; set; }
    }

    public class GpsRouteSummaryDTO
    {
        /// <summary>
        /// Data (no fuso informado) no formato yyyy-MM-dd.
        /// </summary>
        public string Date { get; set; } = string.Empty;

        public DateTime? StartUtc { get; set; }
        public DateTime? EndUtc { get; set; }

        public int TotalPoints { get; set; }
        public double TotalDistanceKm { get; set; }
        public int TotalStops { get; set; }

        public double TotalDurationMinutes { get; set; }
        public double MovingMinutes { get; set; }
        public double StoppedMinutes { get; set; }
    }

    public class GpsRouteDayDTO
    {
        public int ProfessionalId { get; set; }
        public int CompanyId { get; set; }
        public GpsRouteSummaryDTO Summary { get; set; } = new();
        public List<GpsRoutePointDTO> Points { get; set; } = new();
        public List<GpsRouteStopDTO> Stops { get; set; } = new();
    }
}
