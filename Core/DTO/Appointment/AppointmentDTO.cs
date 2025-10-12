using System;

namespace Core.DTO.Appointment
{
    public class AppointmentDTO
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;

        public DateTime Start { get; set; }
        public DateTime End { get; set; }

        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Notes { get; set; }

        public int CompanyId { get; set; }
        public string? CompanyName { get; set; }

        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }

        public int? TeamId { get; set; }
        public string? TeamName { get; set; }

        public int? ProfessionalId { get; set; }
        public string? ProfessionalName { get; set; }

        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }


// Recurrence info
public string? TimeZoneId { get; set; }
public bool IsRecurring { get; set; }
public string? RecurrenceRule { get; set; }
public Guid? SeriesId { get; set; }
public DateTime? RecurrenceEnd { get; set; }
public int? OccurrenceCount { get; set; }
public bool IsException { get; set; }
public DateTime? OriginalStart { get; set; }
public DateTime? OriginalEnd { get; set; }

    }
}