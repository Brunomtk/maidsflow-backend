using Core.Enums.Appointment;
using System;

namespace Core.Models
{
    public class Appointment : BaseModel
    {
        public string Title { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;

        // Datas
        public DateTime Start { get; set; }
        public DateTime End { get; set; }

        // Relacionamentos
        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public int? TeamId { get; set; }
        public Team? Team { get; set; }

        public int? ProfessionalId { get; set; }
        public Professional? Professional { get; set; }

        // Status e tipo
        public AppointmentStatus Status { get; set; }
        public AppointmentType Type { get; set; }

        public string? Notes { get; set; }


// Recurrence fields
public string? TimeZoneId { get; set; } // e.g., "America/Sao_Paulo"
public bool IsRecurring { get; set; }                 // part of a recurrence?
public string? RecurrenceRule { get; set; }           // RRULE iCal
public Guid? SeriesId { get; set; }                   // series identifier
public DateTime? RecurrenceEnd { get; set; }          // series end (UTC)
public int? OccurrenceCount { get; set; }             // COUNT
public bool IsException { get; set; }                 // instance turned into exception?
public DateTime? OriginalStart { get; set; }          // UTC
public DateTime? OriginalEnd { get; set; }            // UTC

    }
}