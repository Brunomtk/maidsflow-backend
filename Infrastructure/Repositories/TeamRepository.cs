using Core.DTO.Teams;
using Core.Enums;
using Core.Models;
using Infrastructure.ServiceExtension;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class TeamRepository : GenericRepository<Team>, ITeamRepository
    {
        private readonly DbContextClass _dbContext;

        public TeamRepository(DbContextClass context) : base(context)
        {
            _dbContext = context;
        }

        /// <summary>
        /// Lista paginada de equipes, com filtros simples e incluindo Members.
        /// </summary>
        public async Task<PagedResult<Team>> GetPagedTeams(
            int page,
            int pageSize,
            string status = "all",
            string? search = null)
        {
            var query = _dbContext.Teams
                .Include(t => t.Members)
                    .ThenInclude(m => m.Professional)
                .Include(t => t.Members)
                    .ThenInclude(m => m.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(t => t.Name.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<StatusEnum>(status, true, out var statusEnum))
                {
                    query = query.Where(t => t.Status == statusEnum);
                }
            }

            query = query.OrderBy(t => t.Name);

            return await query.GetPagedAsync(page, pageSize);
        }

        /// <summary>
        /// Lista paginada usando TeamFiltersDTO, incluindo Members.
        /// </summary>
        public async Task<PagedResult<Team>> GetPagedTeamsFilteredAsync(TeamFiltersDTO filters)
        {
            var query = _dbContext.Teams
                .Include(t => t.Members)
                    .ThenInclude(m => m.Professional)
                .Include(t => t.Members)
                    .ThenInclude(m => m.User)
                .AsQueryable();

            if (filters.CompanyId.HasValue)
            {
                query = query.Where(t => t.CompanyId == filters.CompanyId.Value);
            }

            if (filters.LeaderId.HasValue)
            {
                query = query.Where(t => t.LeaderId == filters.LeaderId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filters.Status) && !string.Equals(filters.Status, "all", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<StatusEnum>(filters.Status, true, out var statusEnum))
                {
                    query = query.Where(t => t.Status == statusEnum);
                }
            }

            if (!string.IsNullOrWhiteSpace(filters.Search))
            {
                query = query.Where(t => t.Name.Contains(filters.Search));
            }

            query = query.OrderBy(t => t.Name);

            return await query.GetPagedAsync(filters.Page, filters.PageSize);
        }

        /// <summary>
        /// Busca única com Members (para o GET /api/Team/{id}).
        /// </summary>
        public async Task<Team?> GetByIdWithMembersAsync(int id)
        {
            return await _dbContext.Teams
                .Include(t => t.Members)
                    .ThenInclude(m => m.Professional)
                .Include(t => t.Members)
                    .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(t => t.Id == id);
        }
    }

    public interface ITeamRepository : IGenericRepository<Team>
    {
        Task<PagedResult<Team>> GetPagedTeams(int page, int pageSize, string status = "all", string? search = null);
        Task<PagedResult<Team>> GetPagedTeamsFilteredAsync(TeamFiltersDTO filters);
        Task<Team?> GetByIdWithMembersAsync(int id);
    }
}
