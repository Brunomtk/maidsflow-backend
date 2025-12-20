using System;

namespace Core.DTO.Plan
{
    public class SwitchCompanyPlanRequest
    {
        public int PlanId { get; set; }
        public bool AutoRenew { get; set; } = false;

        // opcionais (UTC)
        public DateTime? StartDateUtc { get; set; }
        public DateTime? EndDateUtc { get; set; }
    }
}
