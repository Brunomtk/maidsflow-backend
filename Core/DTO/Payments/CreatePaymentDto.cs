using System;
using Core.Enums.Payment;

namespace Core.DTO.Payments
{
    public class CreatePaymentDto
    {
        public int CompanyId { get; set; }
        public int? CustomerId { get; set; }
        public int? CustomerAddressId { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? PaymentDate { get; set; }
        public PaymentStatus Status { get; set; }
        public PaymentMethod? Method { get; set; }
        public string Reference { get; set; } = null!;
        public PaymentFinancialType FinancialType { get; set; } = PaymentFinancialType.Income;
        public int? PaymentCategoryId { get; set; }
        public string? PaymentCategoryName { get; set; }
        public int? PlanId { get; set; }
    }
}
