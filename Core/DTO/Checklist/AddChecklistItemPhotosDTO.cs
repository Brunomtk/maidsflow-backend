using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Core.DTO.Checklist
{
    public class AddChecklistItemPhotosDTO
    {
        [Required] public int ItemId { get; set; }
        [Required] public List<string> Urls { get; set; } = new();
        // Optional parallel descriptions (same count as Urls, if provided)
        public List<string?>? Descriptions { get; set; }
    }
}
