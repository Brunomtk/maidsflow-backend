using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class PushSubscriptionRepository : GenericRepository<PushSubscription>, IPushSubscriptionRepository
    {
        public PushSubscriptionRepository(DbContextClass dbContext) : base(dbContext)
        {
        }

        public async Task<List<PushSubscription>> GetByUserIdAsync(int userId)
        {
            return await _dbContext.Set<PushSubscription>()
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .ToListAsync();
        }

        public async Task<PushSubscription?> GetByUserIdAndEndpointAsync(int userId, string endpoint)
        {
            return await _dbContext.Set<PushSubscription>()
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == endpoint);
        }

        public async Task<List<PushSubscription>> GetByUserIdsAsync(IEnumerable<int> userIds)
        {
            var ids = userIds.Distinct().ToList();
            if (ids.Count == 0) return new List<PushSubscription>();

            return await _dbContext.Set<PushSubscription>()
                .AsNoTracking()
                .Where(s => ids.Contains(s.UserId))
                .ToListAsync();
        }
    }

    public interface IPushSubscriptionRepository : IGenericRepository<PushSubscription>
    {
        Task<List<PushSubscription>> GetByUserIdAsync(int userId);
        Task<PushSubscription?> GetByUserIdAndEndpointAsync(int userId, string endpoint);
        Task<List<PushSubscription>> GetByUserIdsAsync(IEnumerable<int> userIds);
    }
}
