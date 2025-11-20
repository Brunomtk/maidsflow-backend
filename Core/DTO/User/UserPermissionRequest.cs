using Core.Enums.User;

namespace Core.DTO.User
{
    /// <summary>
    /// DTO used to send and edit user permissions via the Users API.
    /// Only the permission code and an optional description are needed.
    /// </summary>
    public class UserPermissionRequest
    {
        public UserPermissionCode Code { get; set; }
        public string? Description { get; set; }
    }
}
