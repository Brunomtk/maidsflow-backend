using Core.DTO.Teams;
using Core.Models;
using Core.Enums;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Core.Exceptions;
using Services.Security;

namespace Services
{
    public class TeamService : ITeamService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public TeamService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IScopeGuard scope)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<PagedResult<Team>> GetPagedTeams(int page, int pageSize, string status = "all", string? search = null)
        {
            // Normalize: always go through the filtered method so we can inject CompanyId scope.
            var filters = new TeamFiltersDTO
            {
                Page = page,
                PageSize = pageSize,
                Status = status,
                Search = search
            };

            return await GetPagedTeams(filters);
        }

        public async Task<PagedResult<Team>> GetPagedTeams(TeamFiltersDTO filters)
        {
            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company inválido.");

                filters.CompanyId = scopedCompanyId.Value;
            }

            return await _unitOfWork.Teams.GetPagedTeamsFilteredAsync(filters);
        }

        public async Task<Team?> GetByIdAsync(int id)
        {
            // Busca com Include(Members)
            var team = await _unitOfWork.Teams.GetByIdWithMembersAsync(id);
            if (team == null)
                return null;

            await _scope.EnsureCompanyAccessAsync(team.CompanyId);

            // Professional role: read-only access.

            // Fallback de segurança:
            if (team.Members == null || team.Members.Count == 0)
            {
                if (_unitOfWork.Teams is ITeamRepository teamRepo)
                {
                    var members = await teamRepo.GetMembersByTeamIdAsync(id);
                    foreach (var m in members)
                        team.Members.Add(m);
                }
            }

            return team;
        }

        public async Task<Team> CreateAsync(Team team)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não pode criar equipes.");

            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company inválido.");

                team.CompanyId = scopedCompanyId.Value;
            }

            await ValidateMembersScopeAsync(team);

            await _unitOfWork.Teams.Add(team);
            await _unitOfWork.SaveAsync();
            return team;
        }

        public async Task<Team?> UpdateAsync(int id, Team updatedTeam)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não pode atualizar equipes.");

            var team = await _unitOfWork.Teams.GetByIdWithMembersAsync(id);
            if (team == null)
                return null;

            await _scope.EnsureCompanyAccessAsync(team.CompanyId);

            // For company users, don't allow moving between companies
            if (!_currentUser.IsAdmin)
                updatedTeam.CompanyId = team.CompanyId;

            await ValidateMembersScopeAsync(updatedTeam);

            // Campos principais
            team.Name = updatedTeam.Name;
            team.Region = updatedTeam.Region;
            team.Description = updatedTeam.Description;
            team.CompanyId = updatedTeam.CompanyId;
            team.Status = updatedTeam.Status;

            // Remove todos os membros atuais diretamente no banco
            if (_unitOfWork.Teams is ITeamRepository teamRepo)
            {
                await teamRepo.RemoveMembersByTeamIdAsync(id);
            }

            // Reconstrói a coleção de membros
            team.Members.Clear();
            if (updatedTeam.Members != null)
            {
                foreach (var member in updatedTeam.Members)
                {
                    team.Members.Add(new TeamMember
                    {
                        TeamId = team.Id,
                        ProfessionalId = member.ProfessionalId,
                        UserId = member.UserId,
                        Description = member.Description,
                        IsLeader = member.IsLeader
                    });
                }
            }

            _unitOfWork.Teams.Update(team);
            await _unitOfWork.SaveAsync();

            var reloaded = await _unitOfWork.Teams.GetByIdWithMembersAsync(id);
            return reloaded ?? team;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não pode remover equipes.");

            var team = await _unitOfWork.Teams.GetById(id);
            if (team == null)
                return false;

            await _scope.EnsureCompanyAccessAsync(team.CompanyId);

            _unitOfWork.Teams.Delete(team);
            await _unitOfWork.SaveAsync();
            return true;
        }

        private async Task ValidateMembersScopeAsync(Team team)
        {
            if (team.Members == null || team.Members.Count == 0) return;

            if (_currentUser.IsAdmin) return; // admin can attach anyone

            var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
            if (!scopedCompanyId.HasValue)
                throw new ForbiddenException("Escopo de company inválido.");

            foreach (var m in team.Members)
            {
                // TeamMember.ProfessionalId é obrigatório (int)
                if (m.ProfessionalId > 0)
                    await _scope.EnsureProfessionalInCompanyAsync(m.ProfessionalId);

                if (m.UserId.HasValue)
                    await _scope.EnsureUserInCompanyAsync(m.UserId.Value);
            }
        }
    }

    public interface ITeamService
    {
        Task<PagedResult<Team>> GetPagedTeams(int page, int pageSize, string status = "all", string? search = null);
        Task<PagedResult<Team>> GetPagedTeams(TeamFiltersDTO filters);
        Task<Team?> GetByIdAsync(int id);
        Task<Team> CreateAsync(Team team);
        Task<Team?> UpdateAsync(int id, Team updatedTeam);
        Task<bool> DeleteAsync(int id);
    }
}
