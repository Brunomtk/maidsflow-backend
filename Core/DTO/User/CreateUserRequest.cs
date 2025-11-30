using Core.Enums;
using System.Collections.Generic;

namespace Core.DTO.User
{
    public class CreateUserRequest
    {
        public required string Name { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }
        public required string Role { get; set; } // "admin", "company", "professional"
        public StatusEnum Status { get; set; } = StatusEnum.Active;
        public int? CompanyId { get; set; }
        public int? ProfessionalId { get; set; }
        
        // Preferências opcionais
        public string? Language { get; set; } // "pt-br" ou "english"
        public string? Theme { get; set; } // "claro" ou "escuro"

                /// <summary>
        /// Indica se o usuário já concluiu o fluxo de onboarding inicial.
        /// </summary>
        public bool Onboarding { get; set; } = false;

        /// <summary>
        /// Optional list of permissions to assign to this user.
        /// </summary>
        public List<UserPermissionRequest>? Permissions { get; set; }
    }
}
