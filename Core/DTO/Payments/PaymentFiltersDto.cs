using System;
using Core.Enums.Payment;

namespace Core.DTO.Payments
{
    public class PaymentFiltersDto
    {
        public int? CompanyId { get; set; }
        public int? CustomerId { get; set; }
        public int? CustomerAddressId { get; set; }
        public PaymentStatus? Status { get; set; }
        public string? Search { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? PlanId { get; set; }
        public PaymentFinancialType? FinancialType { get; set; }
        public int? PaymentCategoryId { get; set; }
        public string? PaymentCategoryName { get; set; }

        // Paging
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
