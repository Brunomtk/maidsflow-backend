using System;

namespace Core.DTO.Plan
{
    public class UpdatePlanSubscriptionRequest
    {
        public bool? AutoRenew { get; set; }

        // opcionais (UTC)
        public DateTime? StartDateUtc { get; set; }
        public DateTime? EndDateUtc { get; set; }

        // aceita número (ex: "0") ou nome (ex: "Cancelled")
        public string? Status { get; set; }
    }
}
