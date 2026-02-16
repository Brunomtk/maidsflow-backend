using System.Collections.Generic;

namespace Core.DTO.Billing
{
    public class StripeBillingSummaryDTO
    {
        public int CompanyId { get; set; }
        public string? StripeCustomerId { get; set; }

        public StripeCustomerWalletDTO? Wallet { get; set; }

        public List<StripeSubscriptionInfoDTO> LatestSubscriptions { get; set; } = new();

        /// <summary>
        /// Next upcoming charge (Stripe Upcoming Invoice), when available.
        /// </summary>
        public StripeUpcomingChargeDTO? UpcomingCharge { get; set; }
    }
}
