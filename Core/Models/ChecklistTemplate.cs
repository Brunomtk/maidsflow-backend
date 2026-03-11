using System.Collections.Generic;

namespace Core.Models
{
    public class ChecklistTemplate : BaseModel
    {
        public int? CompanyId { get; set; }
        public Company? Company { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string TemplateType { get; set; } = "airbnb";
        public bool IsSystemTemplate { get; set; }
        public bool IsActive { get; set; } = true;
        public ICollection<ChecklistTemplateItem> Items { get; set; } = new List<ChecklistTemplateItem>();
    }
}
