// Core/DTO/Cancellation/CancellationFiltersDto.cs
using System;

namespace Core.DTO.Cancellation
{
    public class CancellationFiltersDto
    {
        public string? Search { get; set; }
        public int? CompanyId { get; set; }
        public int? CustomerId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? RefundStatus { get; set; }
    }
}
