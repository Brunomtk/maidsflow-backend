using System;

namespace Core.DTO.Recurrences
{
    public class RecurrenceFiltersDTO
    {
        public string? Status { get; set; }
        public string? Type { get; set; }
        public string? Search { get; set; }

        public int? CompanyId { get; set; }
        public int? TeamId { get; set; }
        public int? CustomerId { get; set; }

        public string? StartDate { get; set; }
        public string? EndDate { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
