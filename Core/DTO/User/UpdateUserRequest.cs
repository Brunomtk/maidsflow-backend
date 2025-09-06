using Core.Enums;

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
    }
}
