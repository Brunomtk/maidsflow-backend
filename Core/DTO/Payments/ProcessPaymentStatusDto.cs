using System;
using Core.Enums.Payment;

namespace Core.DTO.Payments
{
    public class ProcessPaymentStatusDto
    {
        public PaymentStatus Status { get; set; }
        public DateTime? PaymentDate { get; set; }
    }
}
