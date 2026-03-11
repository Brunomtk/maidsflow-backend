using System.Collections.Generic;
using Core.Enums;

namespace Core.Models
{
    public class ChecklistItem : BaseModel
    {
        public int ChecklistId { get; set; }
        public Checklist Checklist { get; set; } = null!;

        public int? CustomerAreaId { get; set; }
        public CustomerArea? CustomerArea { get; set; }

        public int? ChecklistTemplateItemId { get; set; }
        public ChecklistTemplateItem? ChecklistTemplateItem { get; set; }

        public string SpaceName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsRequired { get; set; } = true;
        public bool RequiresPhoto { get; set; }
        public int SortOrder { get; set; }

        public ChecklistItemStatus? Status { get; set; }
        public string? Observacoes { get; set; }
        public ICollection<ChecklistItemPhoto> Photos { get; set; } = new List<ChecklistItemPhoto>();
    }
}
