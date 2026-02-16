using System;

namespace Core.DTO.Billing
{
    public class StripeUpcomingChargeDTO
    {
        /// <summary>
        /// Stripe Upcoming Invoice Id (can be null on some cases).
        /// </summary>
        public string? UpcomingInvoiceId { get; set; }

        public string? SubscriptionId { get; set; }

        public long AmountDue { get; set; }
        public string? Currency { get; set; }

        public DateTime? NextAttemptUtc { get; set; }
        public DateTime? PeriodStartUtc { get; set; }
        public DateTime? PeriodEndUtc { get; set; }
    }
}
