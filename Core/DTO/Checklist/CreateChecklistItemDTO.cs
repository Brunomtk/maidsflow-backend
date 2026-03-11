using System.ComponentModel.DataAnnotations;

namespace Core.DTO.Checklist
{
    public class CreateChecklistItemDTO
    {
        [Required] public int ChecklistId { get; set; }
        public int? CustomerAreaId { get; set; }
        [Required] public string SpaceName { get; set; } = string.Empty;
        [Required] public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsRequired { get; set; } = true;
        public bool RequiresPhoto { get; set; }
        public int SortOrder { get; set; }
        public string? Observacoes { get; set; }
    }
}
