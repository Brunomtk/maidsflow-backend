using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Core.DTO.Issues
{
    public class CreateServiceIssueDTO
    {
        [Required]
        public int AppointmentId { get; set; }

        [Required]
        [MaxLength(60)]
        public string Type { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Summary { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public decimal? EstimatedAmount { get; set; }

        public List<string>? PhotoUrls { get; set; }
    }
}
