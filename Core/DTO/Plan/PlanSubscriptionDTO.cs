using Core.Enums.Plan;

namespace Core.DTO.Plan
{
    public class PlanSubscriptionDTO
    {
        public int Id { get; set; }
        public int PlanId { get; set; }
        public int CompanyId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public PlanSubscriptionStatusEnum Status { get; set; }
        public bool AutoRenew { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
    }
}
