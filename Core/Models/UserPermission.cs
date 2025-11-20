using Core.Enums.User;

namespace Core.Models
{
    /// <summary>
    /// Link table between User and the permissions explicitly granted to that user.
    /// </summary>
    public class UserPermission : BaseModel
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public UserPermissionCode Code { get; set; }

        /// <summary>
        /// Optional human-readable description for UI or debugging.
        /// </summary>
        public string? Description { get; set; }
    }
}
