// Core/Models/Payment.cs
using System;
using Core.Enums.Payment;


namespace Core.Models
{
    public class Payment : BaseModel
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }
        public string? CompanyName { get; set; }

        public decimal Amount { get; set; }

        public DateTime DueDate { get; set; }
        public DateTime? PaymentDate { get; set; }

        public PaymentStatus Status { get; set; }
        public PaymentMethod? Method { get; set; }

        public string Reference { get; set; } = null!;

        public PaymentFinancialType FinancialType { get; set; } = PaymentFinancialType.Income;
        public int? PaymentCategoryId { get; set; }
        public PaymentCategory? PaymentCategory { get; set; }
        public string? PaymentCategoryName { get; set; }

        // Plan agora é opcional (não obrigatório)
        public int? PlanId { get; set; }
        public string? PlanName { get; set; }

        // Customer agora pode ser vinculado ao pagamento (opcional)
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public int? CustomerAddressId { get; set; }
        public CustomerAddress? CustomerAddress { get; set; }
    }
}
