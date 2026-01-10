using Core.Enums.Payroll;
using Core.Enums.Team;

namespace Core.DTO.PayrollRules
{
    public class CreatePayrollRuleDTO
    {
        public int? CompanyId { get; set; }
        public int? ServiceTypeId { get; set; }
        public TeamMemberRole TeamRole { get; set; } = TeamMemberRole.Member;
        public RateType RateType { get; set; } = RateType.Fixed;
        public decimal RateValue { get; set; }
        public int Priority { get; set; } = 0;
        public bool? IsActive { get; set; }
    }
}
