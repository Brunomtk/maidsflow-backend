using System.Collections.Generic;

namespace Core.DTO.ChecklistTemplate
{
    public class ChecklistTemplateDTO
    {
        public int Id { get; set; }
        public int? CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string TemplateType { get; set; } = string.Empty;
        public bool IsSystemTemplate { get; set; }
        public bool IsActive { get; set; }
        public int ItemsCount { get; set; }
        public List<ChecklistTemplateItemDTO> Items { get; set; } = new();
    }

    public class ChecklistTemplateItemDTO
    {
        public int Id { get; set; }
        public string SpaceName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsRequired { get; set; }
        public bool RequiresPhoto { get; set; }
        public int SortOrder { get; set; }
    }
}
