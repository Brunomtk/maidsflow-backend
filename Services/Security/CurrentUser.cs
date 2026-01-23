using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Services.Security
{
    public class CurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _http;

        public CurrentUser(IHttpContextAccessor http)
        {
            _http = http;
        }

        private ClaimsPrincipal? Principal => _http.HttpContext?.User;

        public int UserId
        {
            get
            {
                var v = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                return int.TryParse(v, out var id) ? id : 0;
            }
        }

        public string Role => Principal?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        public int? CompanyId
        {
            get
            {
                var v = Principal?.FindFirstValue("companyId");
                return int.TryParse(v, out var id) ? id : null;
            }
        }

        public int? ProfessionalId
        {
            get
            {
                var v = Principal?.FindFirstValue("professionalId");
                return int.TryParse(v, out var id) ? id : null;
            }
        }

        public int? CustomerId
        {
            get
            {
                var v = Principal?.FindFirstValue("customerId");
                return int.TryParse(v, out var id) ? id : null;
            }
        }

        public bool IsAdmin => string.Equals(Role, "admin", System.StringComparison.OrdinalIgnoreCase);
        public bool IsCompany => string.Equals(Role, "company", System.StringComparison.OrdinalIgnoreCase);
        public bool IsPropertyManager => string.Equals(Role, "propertyManager", System.StringComparison.OrdinalIgnoreCase);
        public bool IsProfessional => string.Equals(Role, "professional", System.StringComparison.OrdinalIgnoreCase);
    }
}
