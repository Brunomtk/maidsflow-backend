// Core/DTO/GpsTracking/GpsTrackingFiltersDTO.cs
using System;
using Core.Enums.GpsTracking;

namespace Core.DTO.GpsTracking
{
    public class GpsTrackingFiltersDTO
    {
 
        public GpsTrackingStatus? Status { get; set; }

        public int? CompanyId { get; set; }
        public int? ProfessionalId { get; set; }
        public string? SearchQuery { get; set; }

        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
