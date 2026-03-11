namespace Core.Models
{
    public class ChecklistTemplateItem : BaseModel
    {
        public int ChecklistTemplateId { get; set; }
        public ChecklistTemplate ChecklistTemplate { get; set; } = null!;
        public string SpaceName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsRequired { get; set; } = true;
        public bool RequiresPhoto { get; set; }
        public int SortOrder { get; set; }
    }
}
