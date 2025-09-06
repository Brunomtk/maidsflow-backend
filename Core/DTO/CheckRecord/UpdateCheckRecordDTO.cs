using System;

namespace Core.DTO.CheckRecord
{
    public class UpdateCheckRecordDTO
    {
        public int? ProfessionalId { get; set; }
        public string? ProfessionalName { get; set; }

        public int? CompanyId { get; set; }

        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }

        public int? AppointmentId { get; set; }

        public string? Address { get; set; }

        public int? TeamId { get; set; }
        public string? TeamName { get; set; }

        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }

        public string? Status { get; set; }

        public string? ServiceType { get; set; }

        public string? Notes { get; set; }
    }
}
