using System;
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

        /// <summary>
        /// S3 object key for the user's avatar (ex: "UserAvatar/10/abcd.png").
        /// Nullable because avatar is optional.
        /// </summary>
        public string? AvatarKey { get; set; }

        public int? CompanyId { get; set; }

        public int? ProfessionalId { get; set; }

        /// <summary>
        /// When Role == "propertyManager", this links the user to a specific Customer.
        /// Property Managers must be scoped to a single Customer.
        /// </summary>
        public int? CustomerId { get; set; }
        
        // Explicit permissions granted to this user
        public ICollection<UserPermission> Permissions { get; set; } = new List<UserPermission>();

        // Password reset (forgot password)
        public string? PasswordResetTokenHash { get; set; }
        public DateTime? PasswordResetTokenExpiresAt { get; set; }

        
        
                // Indica se o usuário já concluiu o fluxo de onboarding inicial
        public bool Onboarding { get; set; } = false;
        
        // Preferência de idioma do usuário: "pt-br" ou "english"
        public string? Language { get; set; }
        
        // Preferência de tema do usuário: "claro" ou "escuro"
        public string? Theme { get; set; }
        
        // Refresh token persistido para fluxo de "lembrar de mim"
        public string? RefreshToken { get; set; }
        
        public DateTime? RefreshTokenExpiresAt { get; set; }
    }
}
