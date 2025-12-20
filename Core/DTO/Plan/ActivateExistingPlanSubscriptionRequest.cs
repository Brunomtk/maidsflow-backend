using System;

namespace Core.DTO.Plan
{
    public class ActivateExistingPlanSubscriptionRequest
    {
        public bool? AutoRenew { get; set; }

        // opcionais (UTC)
        public DateTime? StartDateUtc { get; set; }
        public DateTime? EndDateUtc { get; set; }
    }
}
