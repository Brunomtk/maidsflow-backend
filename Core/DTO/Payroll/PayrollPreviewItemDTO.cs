using System;
using Core.Enums.Payroll;
using Core.Enums.Team;

namespace Core.DTO.Payroll
{
    public class PayrollPreviewItemDTO
    {
        public int AppointmentId { get; set; }

        public DateTime OccurrenceStart { get; set; }
        public DateTime OccurrenceEnd { get; set; }

        // Legacy fields (kept for backward compatibility)
        public DateTime AppointmentStart { get; set; }
        public DateTime AppointmentEnd { get; set; }

        public int CompanyId { get; set; }

        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }

        public int? TeamId { get; set; }
        public string? TeamName { get; set; }

        public int? ServiceTypeId { get; set; }
        public string? ServiceTypeName { get; set; }

        public string? Category { get; set; }

        public int ProfessionalId { get; set; }
        public string? ProfessionalName { get; set; }

        public TeamMemberRole TeamRole { get; set; } = TeamMemberRole.Member;

        public int? PayrollRuleId { get; set; }
        public int? PayrollRulePriority { get; set; }
        public RateType? RateType { get; set; }
        public decimal? RateValue { get; set; }

        /// <summary>
        /// Valor usado como base para cálculo (quando Percent). Por padrão usamos Customer.Ticket.
        /// </summary>
        public decimal SourceAmount { get; set; }

        public decimal CalculatedAmount { get; set; }

        public bool MissingRule { get; set; }
    }
}
