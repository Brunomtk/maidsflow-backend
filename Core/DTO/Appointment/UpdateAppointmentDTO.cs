using Core.Enums.Appointment;
using System;

namespace Core.DTO.Appointment
{
    public class UpdateAppointmentDTO
    {
        public string? Title { get; set; }
        public string? Address { get; set; }

        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }

        public int? CompanyId { get; set; }
        public int? CustomerId { get; set; }
        public int? TeamId { get; set; }
        public int? ProfessionalId { get; set; }

        public AppointmentStatus? Status { get; set; }
        public AppointmentType? Type { get; set; }

        public string? Notes { get; set; }
    }
}
