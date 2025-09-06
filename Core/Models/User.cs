using Core.Enums;

namespace Core.Models
{
    public class User : BaseModel
    {
        public required string Name { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }

        public required string Role { get; set; } // admin, company, professional

        public StatusEnum Status { get; set; } = StatusEnum.Active;

        public string? Avatar { get; set; }

        public int? CompanyId { get; set; }

        public int? ProfessionalId { get; set; }
    }
}
