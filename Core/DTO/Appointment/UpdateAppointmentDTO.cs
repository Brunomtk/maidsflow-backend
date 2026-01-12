using Core.Enums.Appointment;
using System;
using System.Collections.Generic;

namespace Core.DTO.Appointment
{
    public enum RecurrenceScope { This, ThisAndFollowing, All }
}

namespace Core.DTO.Appointment
{
    public class UpdateAppointmentDTO
    {
        public string? Title { get; set; }
        public string? Address { get; set; }

        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }

        // For recurring actions: identify the clicked occurrence within the series
        public DateTime? OccurrenceStart { get; set; }
        public DateTime? OccurrenceEnd { get; set; }

        public int? CompanyId { get; set; }
        public int? CustomerId { get; set; }
        public int? TeamId { get; set; }
        public List<int>? ProfessionalIds { get; set; }

        public AppointmentStatus? Status { get; set; }
        public AppointmentType? Type { get; set; }

        /// <summary>
        /// Categoria livre (ex.: "Residential").
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// FK para ServiceTypes.
        /// </summary>
        public int? ServiceTypeId { get; set; }

        public string? Notes { get; set; }


public string? TimeZoneId { get; set; }

// Recurrence (optional updates)
public bool? IsRecurring { get; set; }
public string? RecurrenceRule { get; set; }
public DateTime? RecurrenceEnd { get; set; }
public int? OccurrenceCount { get; set; }

// Scope for recurrence-aware updates
public RecurrenceScope Scope { get; set; } = RecurrenceScope.This;

    }
}