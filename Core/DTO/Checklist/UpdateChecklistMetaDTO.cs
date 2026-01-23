using System.ComponentModel.DataAnnotations;

namespace Core.DTO.Checklist
{
    public class UpdateChecklistMetaDTO
    {
        [Required] public int ChecklistId { get; set; }
        public int? CustomerAddressId { get; set; }
        public int? AppointmentId { get; set; }
        public int? ProfessionalId { get; set; }
    }
}
