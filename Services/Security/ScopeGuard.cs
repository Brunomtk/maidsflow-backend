using System.Threading.Tasks;
using Core.Exceptions;
using Infrastructure.Repositories;

namespace Services.Security
{
    public class ScopeGuard : IScopeGuard
    {
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork _uow;

        public ScopeGuard(ICurrentUser currentUser, IUnitOfWork uow)
        {
            _currentUser = currentUser;
            _uow = uow;
        }

        public async Task<int?> GetScopedCompanyIdAsync()
        {
            if (_currentUser.IsAdmin) return null;

            if (_currentUser.CompanyId.HasValue) return _currentUser.CompanyId.Value;

            // Fallback: resolve from DB using UserId
            if (_currentUser.UserId <= 0) return null;

            var user = await _uow.Users.GetById(_currentUser.UserId);
            if (user == null) return null;

            if (user.CompanyId.HasValue) return user.CompanyId.Value;

            if (user.ProfessionalId.HasValue)
            {
                var prof = await _uow.Professionals.GetByIdAsync(user.ProfessionalId.Value);
                return prof?.CompanyId;
            }

            return null;
        }

        public async Task<int?> GetScopedProfessionalIdAsync()
        {
            if (_currentUser.IsAdmin) return null;

            if (_currentUser.ProfessionalId.HasValue) return _currentUser.ProfessionalId.Value;

            if (_currentUser.UserId <= 0) return null;

            var user = await _uow.Users.GetById(_currentUser.UserId);
            return user?.ProfessionalId;
        }

        public async Task EnsureCompanyAccessAsync(int companyId)
        {
            if (_currentUser.IsAdmin) return;

            var scoped = await GetScopedCompanyIdAsync();
            if (!scoped.HasValue || scoped.Value != companyId)
                throw new ForbiddenException("Você não tem permissão para acessar esta company.");
        }

        public async Task EnsureProfessionalAccessAsync(int professionalId)
        {
            if (_currentUser.IsAdmin) return;

            var scopedProfessionalId = await GetScopedProfessionalIdAsync();
            if (_currentUser.IsProfessional)
            {
                if (!scopedProfessionalId.HasValue || scopedProfessionalId.Value != professionalId)
                    throw new ForbiddenException("Você não tem permissão para acessar este profissional.");
                return;
            }

            // company user: ensure the professional belongs to the same company
            if (_currentUser.IsCompany)
            {
                await EnsureProfessionalInCompanyAsync(professionalId);
                return;
            }

            // other roles: default forbid
            throw new ForbiddenException("Você não tem permissão para acessar este profissional.");
        }

        public async Task EnsureProfessionalInCompanyAsync(int professionalId)
        {
            if (_currentUser.IsAdmin) return;

            var scopedCompanyId = await GetScopedCompanyIdAsync();
            if (!scopedCompanyId.HasValue)
                throw new ForbiddenException("Escopo de company inválido.");

            var prof = await _uow.Professionals.GetByIdAsync(professionalId);
            if (prof == null || prof.CompanyId != scopedCompanyId.Value)
                throw new ForbiddenException("Profissional não pertence à sua company.");
        }

        public async Task EnsureUserInCompanyAsync(int userId)
        {
            if (_currentUser.IsAdmin) return;

            var scopedCompanyId = await GetScopedCompanyIdAsync();
            if (!scopedCompanyId.HasValue)
                throw new ForbiddenException("Escopo de company inválido.");

            var user = await _uow.Users.GetById(userId);
            if (user == null || user.CompanyId != scopedCompanyId.Value)
                throw new ForbiddenException("Usuário não pertence à sua company.");
        }

        public async Task EnsureCustomerInCompanyAsync(int customerId)
        {
            if (_currentUser.IsAdmin) return;

            var scopedCompanyId = await GetScopedCompanyIdAsync();
            if (!scopedCompanyId.HasValue)
                throw new ForbiddenException("Escopo de company inválido.");

            var customer = await _uow.Customers.GetByIdAsync(customerId);
            if (customer == null || customer.CompanyId != scopedCompanyId.Value)
                throw new ForbiddenException("Cliente não pertence à sua company.");
        }

        public async Task EnsureTeamInCompanyAsync(int teamId)
        {
            if (_currentUser.IsAdmin) return;

            var scopedCompanyId = await GetScopedCompanyIdAsync();
            if (!scopedCompanyId.HasValue)
                throw new ForbiddenException("Escopo de company inválido.");

            var team = await _uow.Teams.GetById(teamId);
            if (team == null || team.CompanyId != scopedCompanyId.Value)
                throw new ForbiddenException("Equipe não pertence à sua company.");
        }

        public async Task EnsureAppointmentAccessAsync(int appointmentId)
        {
            if (_currentUser.IsAdmin) return;

            var scopedCompanyId = await GetScopedCompanyIdAsync();
            if (!scopedCompanyId.HasValue)
                throw new ForbiddenException("Escopo de company inválido.");

            var appt = await _uow.Appointments.GetById(appointmentId);
            if (appt == null || appt.CompanyId != scopedCompanyId.Value)
                throw new ForbiddenException("Agendamento não pertence à sua company.");

            if (_currentUser.IsProfessional)
            {
                var profId = await GetScopedProfessionalIdAsync();
                if (!profId.HasValue)
                    throw new ForbiddenException("Escopo de profissional inválido.");

                // 1) appointment has explicit professionals
                if (appt.ProfessionalIds != null && appt.ProfessionalIds.Count > 0)
                {
                    if (!appt.ProfessionalIds.Contains(profId.Value))
                        throw new ForbiddenException("Você não tem permissão para acessar este agendamento.");
                    return;
                }

                // 2) appointment linked to a team: check membership
                if (appt.TeamId.HasValue)
                {
                    if (_uow.Teams is ITeamRepository teamRepo)
                    {
                        var members = await teamRepo.GetMembersByTeamIdAsync(appt.TeamId.Value);
                        if (members.Exists(m => m.ProfessionalId == profId.Value))
                            return;
                    }

                    throw new ForbiddenException("Você não tem permissão para acessar este agendamento.");
                }

                // If no professional/team binding exists, default deny for professional.
                throw new ForbiddenException("Você não tem permissão para acessar este agendamento.");
            }
        }

        public Task EnsureUserSelfOrAdminAsync(int userId)
        {
            if (_currentUser.IsAdmin) return Task.CompletedTask;

            if (_currentUser.UserId != userId)
                throw new ForbiddenException("Você não tem permissão para acessar este usuário.");

            return Task.CompletedTask;
        }
    }
}
