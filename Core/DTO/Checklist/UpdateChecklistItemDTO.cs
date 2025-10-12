using System.ComponentModel.DataAnnotations;
using Core.Enums;

namespace Core.DTO.Checklist
{
    public class UpdateChecklistItemDTO
    {
        [Required] public int ItemId { get; set; }
        [Required] public ChecklistItemStatus Status { get; set; }
        public string? Observacoes { get; set; }
    }
}
