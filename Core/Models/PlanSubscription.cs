using Core.Enums.Plan;

namespace Core.Models
{
    public class PlanSubscription : BaseModel
    {
        public int PlanId { get; set; }
        public virtual Plan Plan { get; set; }

        public int CompanyId { get; set; }
        public virtual Company Company { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public PlanSubscriptionStatusEnum Status { get; set; } = PlanSubscriptionStatusEnum.Active;

        public bool AutoRenew { get; set; } = false;
    }
}
