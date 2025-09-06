using Core.DTO;
using Core.Models;
using Infrastructure.ServiceExtension;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class PlanSubscriptionRepository : GenericRepository<PlanSubscription>, IPlanSubscriptionRepository
    {
        public PlanSubscriptionRepository(DbContextClass dbContext) : base(dbContext)
        {
        }

        public async Task<PagedResult<PlanSubscription>> GetSubscribersPaged(int planId, int page, int pageSize)
        {
            var query = _dbContext.Set<PlanSubscription>()
                .Include(s => s.Company)
                .Include(s => s.Plan) 
                .Where(s => s.PlanId == planId)
                .AsNoTracking();

            return await query
                .OrderByDescending(s => s.StartDate)
                .GetPagedAsync(page, pageSize);
        }
    }

    public interface IPlanSubscriptionRepository : IGenericRepository<PlanSubscription>
    {
        Task<PagedResult<PlanSubscription>> GetSubscribersPaged(int planId, int page, int pageSize);
    }
}
