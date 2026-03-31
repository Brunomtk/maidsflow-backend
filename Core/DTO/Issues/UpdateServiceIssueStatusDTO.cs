using System.ComponentModel.DataAnnotations;

namespace Core.DTO.Issues
{
    public class UpdateServiceIssueStatusDTO
    {
        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? InternalNotes { get; set; }

        public decimal? ApprovedAmount { get; set; }
    }
}
