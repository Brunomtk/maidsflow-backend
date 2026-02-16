using System;

namespace Core.DTO.Billing
{
    public class StripeSubscriptionInfoDTO
    {
        public string? SubscriptionId { get; set; }
        public string? Status { get; set; }
        public bool CancelAtPeriodEnd { get; set; }

        public DateTime? CurrentPeriodStartUtc { get; set; }
        public DateTime? CurrentPeriodEndUtc { get; set; }

        public string? PriceId { get; set; }
        public string? ProductName { get; set; }
        public long UnitAmount { get; set; }
        public string? Currency { get; set; }
        public string? Interval { get; set; }

        public DateTime? CreatedAtUtc { get; set; }
        public DateTime? CanceledAtUtc { get; set; }
    }
}
