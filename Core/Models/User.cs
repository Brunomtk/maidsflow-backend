using System.Collections.Generic;
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
        
        // Explicit permissions granted to this user
        public ICollection<UserPermission> Permissions { get; set; } = new List<UserPermission>();
        
        
        // Preferência de idioma do usuário: "pt-br" ou "english"
        public string? Language { get; set; }
        
        // Preferência de tema do usuário: "claro" ou "escuro"
        public string? Theme { get; set; }
        
        // Refresh token persistido para fluxo de "lembrar de mim"
        public string? RefreshToken { get; set; }
        
        public DateTime? RefreshTokenExpiresAt { get; set; }
    }
}
