using System;
using Core.Enums;

namespace Core.DTO.Checklist
{
    public class ChecklistFiltersDTO
    {
        public int? CustomerId { get; set; }
        public int? CompanyId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public ChecklistStatus? Status { get; set; }

        public int? AppointmentId { get; set; }
        public int? ProfessionalId { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
