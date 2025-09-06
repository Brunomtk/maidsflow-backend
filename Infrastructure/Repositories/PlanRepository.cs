using Core.DTO;
using Core.Models;
using Infrastructure.ServiceExtension;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class PlanRepository : GenericRepository<Plan>, IPlanRepository
    {
        public PlanRepository(DbContextClass dbContext) : base(dbContext) { }

        public async Task<PagedResult<Plan>> GetPlansPaged(FiltersDTO filtersDTO)
        {
            var query = _dbContext.Set<Plan>().AsQueryable();

            if (!string.IsNullOrEmpty(filtersDTO.Name))
            {
                var name = filtersDTO.Name.ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(name) ||
                    p.Features.Any(f => f.ToLower().Contains(name)));
            }

            return await query
                .AsNoTracking()
                .OrderByDescending(p => p.CreatedDate)
                .GetPagedAsync(filtersDTO.pageNumber, filtersDTO.pageSize);
        }
    }

    public interface IPlanRepository : IGenericRepository<Plan>
    {
        Task<PagedResult<Plan>> GetPlansPaged(FiltersDTO filtersDTO);
    }
}
