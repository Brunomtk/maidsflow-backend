using System.Security.Claims;

namespace Services.Security
{
    public interface ICurrentUser
    {
        int UserId { get; }
        string Role { get; }
        int? CompanyId { get; }
        int? ProfessionalId { get; }
        int? CustomerId { get; }
        bool IsAdmin { get; }
        bool IsCompany { get; }
        bool IsPropertyManager { get; }
        bool IsProfessional { get; }
    }
}
