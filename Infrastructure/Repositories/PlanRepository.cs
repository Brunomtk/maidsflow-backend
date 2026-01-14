using Core.DTO;
using Core.Models;
using Infrastructure.ServiceExtension;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class PlanRepository : GenericRepository<Plan>, IPlanRepository
    {
        public PlanRepository(DbContextClass dbContext) : base(dbContext) { }

        public async Task<Plan?> GetByIdWithCompaniesAsync(int id)
        {
            return await _dbContext.Set<Plan>()
                .Include(p => p.Subscriptions!)
                    .ThenInclude(s => s.Company)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<IEnumerable<Plan>> GetAllWithCompaniesAsync()
        {
            return await _dbContext.Set<Plan>()
                .Include(p => p.Subscriptions!)
                    .ThenInclude(s => s.Company)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Plan?> GetByStripePriceIdAsync(string stripePriceId)
        {
            if (string.IsNullOrWhiteSpace(stripePriceId)) return null;

            return await _dbContext.Set<Plan>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.StripePriceId == stripePriceId);
        }

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

        Task<Plan?> GetByIdWithCompaniesAsync(int id);
        Task<IEnumerable<Plan>> GetAllWithCompaniesAsync();
        Task<Plan?> GetByStripePriceIdAsync(string stripePriceId);
    }
}
