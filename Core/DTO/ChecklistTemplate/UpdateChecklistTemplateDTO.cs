using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Core.DTO.ChecklistTemplate
{
    public class UpdateChecklistTemplateDTO
    {
        [Required] public int Id { get; set; }
        [Required] public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string TemplateType { get; set; } = "airbnb";
        public bool IsActive { get; set; } = true;
        public List<UpdateChecklistTemplateItemDTO> Items { get; set; } = new();
    }

    public class UpdateChecklistTemplateItemDTO
    {
        public int? Id { get; set; }
        [Required] public string SpaceName { get; set; } = string.Empty;
        [Required] public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsRequired { get; set; } = true;
        public bool RequiresPhoto { get; set; }
        public int SortOrder { get; set; }
    }
}
