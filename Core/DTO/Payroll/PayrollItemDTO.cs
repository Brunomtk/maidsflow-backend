using System;
using Core.Enums.Payroll;
using Core.Enums.Team;

namespace Core.DTO.Payroll
{
    public class PayrollItemDTO
    {
        public int Id { get; set; }
        public int PayrollRunId { get; set; }

        public int CompanyId { get; set; }

        public int ProfessionalId { get; set; }
        public string? ProfessionalName { get; set; }

        public int AppointmentId { get; set; }

        public DateTime? OccurrenceStart { get; set; }
        public DateTime? OccurrenceEnd { get; set; }

        // Legacy fields (kept for backward compatibility)
        public DateTime? AppointmentStart { get; set; }
        public DateTime? AppointmentEnd { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }

        public int? ServiceTypeId { get; set; }
        public string? ServiceTypeName { get; set; }

        public string? Category { get; set; }

        public TeamMemberRole TeamRole { get; set; }

        public int? PayrollRuleId { get; set; }
        public int? PayrollRulePriority { get; set; }
        public RateType? RateType { get; set; }
        public decimal? RateValue { get; set; }

        public decimal SourceAmount { get; set; }
        public decimal CalculatedAmount { get; set; }

        public bool MissingRule { get; set; }

        public DateTime CreatedDate { get; set; }
    }
}
