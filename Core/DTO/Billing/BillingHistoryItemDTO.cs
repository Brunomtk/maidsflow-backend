using System;

namespace Core.DTO.Billing
{
    /// <summary>
    /// Item do histórico de cobrança (Stripe Invoice) exibido no "Billing History".
    /// Valores monetários são em cents (ex.: 1000 = R$ 10,00), seguindo o padrão Stripe.
    /// </summary>
    public class BillingHistoryItemDTO
    {
        public required string InvoiceId { get; set; }
        public string? Number { get; set; }
        public string? Status { get; set; }
        public bool Paid { get; set; }

        public long AmountDue { get; set; }
        public long AmountPaid { get; set; }
        public long AmountRemaining { get; set; }
        public string Currency { get; set; } = "usd";

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? PaidAtUtc { get; set; }
        public DateTime? PeriodStartUtc { get; set; }
        public DateTime? PeriodEndUtc { get; set; }

        public string? SubscriptionId { get; set; }
        public string? HostedInvoiceUrl { get; set; }
        public string? InvoicePdfUrl { get; set; }
    }
}
