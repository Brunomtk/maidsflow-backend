using System;
using Core.Enums.Payment;

namespace Core.DTO.Payments
{
    public class PaymentFiltersDto
    {
        public int? CompanyId { get; set; }
        public PaymentStatus? Status { get; set; }
        public string? Search { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? PlanId { get; set; }

        // Paging
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
