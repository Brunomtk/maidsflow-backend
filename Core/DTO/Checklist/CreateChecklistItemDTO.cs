using System.ComponentModel.DataAnnotations;

namespace Core.DTO.Checklist
{
    public class CreateChecklistItemDTO
    {
        [Required] public int ChecklistId { get; set; }
        [Required] public int CustomerAreaId { get; set; }
        public string? Observacoes { get; set; }
    }
}
