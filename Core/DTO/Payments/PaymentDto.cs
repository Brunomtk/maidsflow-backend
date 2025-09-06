// Core/DTO/Payments/PaymentDto.cs
using System;
using Core.Enums.Payment;

namespace Core.DTO.Payments
{
    public class PaymentDto
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }
        public string CompanyName { get; set; }

        public decimal Amount { get; set; }

        public DateTime DueDate { get; set; }
        public DateTime? PaymentDate { get; set; }

        public PaymentStatus Status { get; set; }
        public PaymentMethod? Method { get; set; }

        public string Reference { get; set; }

        public int PlanId { get; set; }
        public string PlanName { get; set; }

        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
    }
}
