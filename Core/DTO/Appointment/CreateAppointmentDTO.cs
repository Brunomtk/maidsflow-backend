using Core.Enums.Appointment;
using System;
using System.Collections.Generic;

namespace Core.DTO.Appointment
{
    public class CreateAppointmentDTO
    {
        public string Title { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;

        public DateTime Start { get; set; }
        public DateTime End { get; set; }

        public int CompanyId { get; set; }
        public int? CustomerId { get; set; }
        public int? TeamId { get; set; }
        public List<int>? ProfessionalIds { get; set; }

        public AppointmentStatus? Status { get; set; }
        public AppointmentType? Type { get; set; }

        /// <summary>
        /// Categoria livre usada pelo front (ex.: "Residential", "Commercial").
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// FK para ServiceTypes (quando o agendamento estiver vinculado a um tipo de serviço).
        /// </summary>
        public int? ServiceTypeId { get; set; }

        public string? Notes { get; set; }


public string? TimeZoneId { get; set; }        // e.g., "America/Sao_Paulo"

// Recurrence
public bool IsRecurring { get; set; }
public string? RecurrenceRule { get; set; } // iCal RRULE
public DateTime? RecurrenceEnd { get; set; } // local time (UI)
public int? OccurrenceCount { get; set; }    // COUNT

    }
}