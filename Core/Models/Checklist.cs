using System.Collections.Generic;
using Core.Enums;

namespace Core.Models
{
    public class Checklist : BaseModel
    {
        public int? AppointmentId { get; set; }
        public Appointment? Appointment { get; set; }

        public int? ProfessionalId { get; set; }
        public Professional? Professional { get; set; }

        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        public int? CustomerAddressId { get; set; }
        public CustomerAddress? CustomerAddress { get; set; }

        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public int? ChecklistTemplateId { get; set; }
        public ChecklistTemplate? ChecklistTemplate { get; set; }
        public string? TemplateNameSnapshot { get; set; }
        public string? PropertyLabel { get; set; }

        public ChecklistStatus Status { get; set; } = ChecklistStatus.EmAndamento;
        public string? ObservacoesGerais { get; set; }
        public ICollection<ChecklistItem> Items { get; set; } = new List<ChecklistItem>();
    }
}
