using Core.DTO.Teams;
using Core.Enums;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using System;
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

        public async Task<PagedResult<Team>> GetPagedTeams(int page, int pageSize, string status = "all", string? search = null)
        {
            return await _unitOfWork.Teams.GetPagedTeams(page, pageSize, status, search);
        }

        public async Task<PagedResult<Team>> GetPagedTeams(TeamFiltersDTO filters)
        {
            return await _unitOfWork.Teams.GetPagedTeamsFilteredAsync(filters);
        }

        public async Task<Team?> GetByIdAsync(int id)
        {
            return await _unitOfWork.Teams.GetById(id);
        }

        public async Task<Team> CreateAsync(Team team)
        {
            await _unitOfWork.Teams.Add(team);
            await _unitOfWork.SaveAsync();
            return team;
        }

        public async Task<Team?> UpdateAsync(int id, Team teamData)
        {
            var team = await _unitOfWork.Teams.GetById(id);
            if (team == null) return null;

            team.Name = teamData.Name;
            team.Description = teamData.Description;
            team.LeaderId = teamData.LeaderId;
            team.Status = teamData.Status;
            team.Region = teamData.Region;
            team.UpdatedDate = DateTime.UtcNow;

            _unitOfWork.Teams.Update(team);
            await _unitOfWork.SaveAsync();
            return team;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var team = await _unitOfWork.Teams.GetById(id);
            if (team == null) return false;

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
        Task<Team?> UpdateAsync(int id, Team teamData);
        Task<bool> DeleteAsync(int id);
    }
}
