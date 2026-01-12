using Core.Enums.Appointment;

namespace Core.DTO.Appointment
{
    public class AppointmentFiltersDTO
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public int? CompanyId { get; set; }
        public int? CustomerId { get; set; }
        public int? TeamId { get; set; }
        public int? ProfessionalId { get; set; }

        public AppointmentStatus? Status { get; set; }
        public AppointmentType? Type { get; set; }

        public string? Search { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
