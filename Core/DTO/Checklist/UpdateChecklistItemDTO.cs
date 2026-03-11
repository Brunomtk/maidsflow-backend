using System.ComponentModel.DataAnnotations;
using Core.Enums;

namespace Core.DTO.Checklist
{
    public class UpdateChecklistItemDTO
    {
        [Required] public int ItemId { get; set; }
        [Required] public ChecklistItemStatus Status { get; set; }
        public string? Observacoes { get; set; }
        public string? SpaceName { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public bool? IsRequired { get; set; }
        public bool? RequiresPhoto { get; set; }
        public int? SortOrder { get; set; }
    }
}
