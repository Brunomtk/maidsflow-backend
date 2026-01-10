using System;
using Core.Enums.Payroll;
using Core.Enums.Team;

namespace Core.Models
{
    public class PayrollItem
    {
        public int Id { get; set; }

        public int PayrollRunId { get; set; }
        public PayrollRun? PayrollRun { get; set; }

        public int CompanyId { get; set; }

        public int ProfessionalId { get; set; }
        public Professional? Professional { get; set; }

        public int AppointmentId { get; set; }
        public Appointment? Appointment { get; set; }

        /// <summary>
        /// Occurrence window (for recurring appointments, the same AppointmentId can repeat).
        /// </summary>
        public DateTime OccurrenceStart { get; set; }
        public DateTime OccurrenceEnd { get; set; }

        public int? AppointmentCompletionId { get; set; }
        public AppointmentCompletion? AppointmentCompletion { get; set; }

        public int? ServiceTypeId { get; set; }
        public ServiceType? ServiceType { get; set; }

        public string? Category { get; set; }

        public TeamMemberRole TeamRole { get; set; }

        public int? PayrollRuleId { get; set; }
        public PayrollRule? PayrollRule { get; set; }

        public int? PayrollRulePriority { get; set; }

        public RateType? RateType { get; set; }
        public decimal? RateValue { get; set; }

        public decimal SourceAmount { get; set; }
        public decimal CalculatedAmount { get; set; }

        public bool MissingRule { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
