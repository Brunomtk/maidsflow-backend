using Core.Enums.CheckRecord;
using System;

namespace Core.Models
{
    public class CheckRecord : BaseModel
    {
        public int ProfessionalId { get; set; }
        public string? ProfessionalName { get; set; }

        public int CompanyId { get; set; }

        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }

        public int AppointmentId { get; set; }

        public string Address { get; set; } = string.Empty;

        public int? TeamId { get; set; }
        public string? TeamName { get; set; }

        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }

        public CheckRecordStatus Status { get; set; } = CheckRecordStatus.Pending;

        public string ServiceType { get; set; } = string.Empty;

        public string? Notes { get; set; }
    }
}
