using System.ComponentModel.DataAnnotations;

namespace Core.DTO.Checklist
{
    public class CreateChecklistDTO
    {
        [Required] public int CustomerId { get; set; }
        public int? CustomerAddressId { get; set; }
        [Required] public int CompanyId { get; set; }
        public string? PropertyLabel { get; set; }
        public string? ObservacoesGerais { get; set; }
        public int? AppointmentId { get; set; }
        public int? ProfessionalId { get; set; }
        public int? ChecklistTemplateId { get; set; }
        public bool AutoPopulateFromTemplate { get; set; } = true;
    }
}
