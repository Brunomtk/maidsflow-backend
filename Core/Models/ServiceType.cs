using System.ComponentModel.DataAnnotations;

namespace Core.Models
{
    public class ServiceType : BaseModel
    {
        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        [MaxLength(500)]
        public string? Description { get; set; }
    }
}
