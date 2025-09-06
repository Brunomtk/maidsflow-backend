using System;
using Core.Enums.Payment;

namespace Core.DTO.Payments
{
    public class CreatePaymentDto
    {
        public int CompanyId { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? PaymentDate { get; set; }
        public PaymentStatus Status { get; set; }
        public PaymentMethod? Method { get; set; }
        public string Reference { get; set; } = null!;
        public int PlanId { get; set; }
    }
}
