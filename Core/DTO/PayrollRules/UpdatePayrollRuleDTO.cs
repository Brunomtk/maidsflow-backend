using Core.Enums.Payroll;
using Core.Enums.Team;

namespace Core.DTO.PayrollRules
{
    public class UpdatePayrollRuleDTO
    {
        public int? ServiceTypeId { get; set; }
        public TeamMemberRole? TeamRole { get; set; }
        public RateType? RateType { get; set; }
        public decimal? RateValue { get; set; }
        public int? Priority { get; set; }
        public bool? IsActive { get; set; }
    }
}
