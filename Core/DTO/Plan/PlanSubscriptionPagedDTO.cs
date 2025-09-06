using System.Collections.Generic;

namespace Core.DTO.Plan
{
    public class PlanSubscriptionPagedDTO
    {
        public int PageCount { get; set; }
        public List<PlanSubscriptionDTO> Result { get; set; } = new();
    }
}
