using System.ComponentModel.DataAnnotations;

namespace Core.DTO.Checklist
{
    public class CreateChecklistDTO
    {
        [Required]
        public int CustomerId { get; set; }
        public string? ObservacoesGerais { get; set; }

        // Metadados opcionais
        public int? AppointmentId { get; set; }
        public int? ProfessionalId { get; set; }
    }
}
