using Core.DTO.Teams;
using Core.Enums;
using Core.Models;
using Infrastructure.ServiceExtension;
using Microsoft.EntityFrameworkCore;
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

        public async Task<PagedResult<Team>> GetPagedTeams(int page, int pageSize, string status = "all", string? search = null)
        {
            var query = _dbContext.Teams
                .Include(t => t.Company)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && status.ToLower() != "all")
            {
                StatusEnum? statusEnum = status.ToLower() switch
                {
                    "ativo" => StatusEnum.Active,
                    "inativo" => StatusEnum.Inactive,
                    _ => null
                };

                if (statusEnum.HasValue)
                    query = query.Where(t => t.Status == statusEnum.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(t => t.Name.ToLower().Contains(search.ToLower()));
            }

            return await query.OrderByDescending(t => t.CreatedDate)
                              .GetPagedAsync(page, pageSize);
        }

        public async Task<PagedResult<Team>> GetPagedTeamsFilteredAsync(TeamFiltersDTO filters)
        {
            var query = _dbContext.Teams
                .Include(t => t.Company)
                .AsQueryable();

            if (filters.CompanyId.HasValue)
                query = query.Where(t => t.CompanyId == filters.CompanyId.Value);

            if (filters.LeaderId.HasValue)
                query = query.Where(t => t.LeaderId == filters.LeaderId.Value);

            if (!string.IsNullOrEmpty(filters.Status) && filters.Status.ToLower() != "all")
            {
                StatusEnum? statusEnum = filters.Status.ToLower() switch
                {
                    "ativo" => StatusEnum.Active,
                    "inativo" => StatusEnum.Inactive,
                    _ => null
                };

                if (statusEnum.HasValue)
                    query = query.Where(t => t.Status == statusEnum.Value);
            }

            if (!string.IsNullOrWhiteSpace(filters.Search))
            {
                query = query.Where(t => t.Name.ToLower().Contains(filters.Search.ToLower()));
            }

            return await query.OrderByDescending(t => t.CreatedDate)
                              .GetPagedAsync(filters.Page, filters.PageSize);
        }
    }

    public interface ITeamRepository : IGenericRepository<Team>
    {
        Task<PagedResult<Team>> GetPagedTeams(int page, int pageSize, string status = "all", string? search = null);
        Task<PagedResult<Team>> GetPagedTeamsFilteredAsync(TeamFiltersDTO filters);
    }
}
