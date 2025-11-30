using Core.Enums;
using System.Collections.Generic;

namespace Core.DTO.User
{
    public class UpdateUserRequest
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? Role { get; set; }
        public StatusEnum? Status { get; set; }
        public int? CompanyId { get; set; }
        public int? ProfessionalId { get; set; }

        public bool? Onboarding { get; set; }

        /// <summary>
        /// Optional list of permissions to replace the current user's permissions.
        /// If null, permissions will not be changed.
        /// </summary>
        public List<UserPermissionRequest>? Permissions { get; set; }
    }
}
