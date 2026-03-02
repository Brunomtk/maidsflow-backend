using System;

namespace Core.DTO.Billing
{
    public class CancelStripeSubscriptionResponse
    {
        public string StripeSubscriptionId { get; set; } = string.Empty;

        public bool CancelAtPeriodEnd { get; set; }

        public DateTime? CurrentPeriodEnd { get; set; }

        public string StripeStatus { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }
}
