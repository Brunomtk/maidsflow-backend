using System.Threading.Tasks;

namespace Services.Security
{
    public interface IScopeGuard
    {
        Task<int?> GetScopedCompanyIdAsync();
        Task<int?> GetScopedProfessionalIdAsync();
        Task<int?> GetScopedCustomerIdAsync();

        Task EnsureCompanyAccessAsync(int companyId);
        Task EnsureProfessionalAccessAsync(int professionalId);

        /// <summary>
        /// Ensures a professional belongs to the current company scope (for company users).
        /// Admin always allowed.
        /// </summary>
        Task EnsureProfessionalInCompanyAsync(int professionalId);

        /// <summary>
        /// Ensures the current user is accessing their own userId (or admin).
        /// </summary>

        Task EnsureUserInCompanyAsync(int userId);
        Task EnsureCustomerInCompanyAsync(int customerId);
        Task EnsureCustomerAddressAccessAsync(int customerAddressId);
        Task EnsureTeamInCompanyAsync(int teamId);
        Task EnsureAppointmentAccessAsync(int appointmentId);
        Task EnsureUserSelfOrAdminAsync(int userId);
    }
}
