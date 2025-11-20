using Core.DTO.Teams;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using System.Threading.Tasks;

namespace Services
{
    public class TeamService : ITeamService
    {
        private readonly IUnitOfWork _unitOfWork;

        public TeamService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public Task<PagedResult<Team>> GetPagedTeams(int page, int pageSize, string status = "all", string? search = null)
        {
            return _unitOfWork.Teams.GetPagedTeams(page, pageSize, status, search);
        }

        public Task<PagedResult<Team>> GetPagedTeams(TeamFiltersDTO filters)
        {
            return _unitOfWork.Teams.GetPagedTeamsFilteredAsync(filters);
        }

        public Task<Team?> GetByIdAsync(int id)
        {
            return _unitOfWork.Teams.GetByIdWithMembersAsync(id);
        }

        public async Task<Team> CreateAsync(Team team)
        {
            _unitOfWork.Teams.Add(team);
            await _unitOfWork.SaveAsync();
            return team;
        }

        /// <summary>
        /// Atualiza uma equipe existente usando a entidade Team já montada.
        /// </summary>
        public async Task<Team?> UpdateAsync(int id, Team updatedTeam)
        {
            var team = await _unitOfWork.Teams.GetByIdWithMembersAsync(id);
            if (team == null)
                return null;

            // Campos principais
            team.Name = updatedTeam.Name;
            team.Region = updatedTeam.Region;
            team.Description = updatedTeam.Description;
            team.CompanyId = updatedTeam.CompanyId;
            team.LeaderId = updatedTeam.LeaderId;

            // Recria os members se vierem preenchidos
            if (updatedTeam.Members != null)
            {
                team.Members.Clear();

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

            return team;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var team = await _unitOfWork.Teams.GetById(id);
            if (team == null)
                return false;

            _unitOfWork.Teams.Delete(team);
            await _unitOfWork.SaveAsync();
            return true;
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
