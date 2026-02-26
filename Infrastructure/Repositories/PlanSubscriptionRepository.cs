using Core.DTO;
using Core.Models;
using Infrastructure.ServiceExtension;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

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

        public async Task<PlanSubscription?> GetActiveByCompanyAsync(int companyId)
        {
            return await _dbContext.Set<PlanSubscription>()
                .Include(s => s.Plan)
                .Include(s => s.Company)
                .Where(s => s.CompanyId == companyId && s.Status == Core.Enums.Plan.PlanSubscriptionStatusEnum.Active)
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync();
        }

        public async Task<List<PlanSubscription>> GetByCompanyAsync(int companyId)
        {
            return await _dbContext.Set<PlanSubscription>()
                .Include(s => s.Plan)
                .Include(s => s.Company)
                .Where(s => s.CompanyId == companyId)
                .OrderByDescending(s => s.StartDate)
                .ToListAsync();
        }

        public async Task<PlanSubscription?> GetByStripeSubscriptionIdAsync(string stripeSubscriptionId)
        {
            if (string.IsNullOrWhiteSpace(stripeSubscriptionId)) return null;

            return await _dbContext.Set<PlanSubscription>()
                .Include(s => s.Plan)
                .Include(s => s.Company)
                .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId);
        }

        public async Task<List<PlanSubscription>> GetAllByStripeSubscriptionIdAsync(string stripeSubscriptionId)
        {
            if (string.IsNullOrWhiteSpace(stripeSubscriptionId)) return new List<PlanSubscription>();

            return await _dbContext.Set<PlanSubscription>()
                .Include(s => s.Plan)
                .Include(s => s.Company)
                .Where(s => s.StripeSubscriptionId == stripeSubscriptionId)
                .OrderByDescending(s => s.EndDate)
                .ThenByDescending(s => s.StartDate)
                .ToListAsync();
        }



        public async Task<List<PlanSubscription>> GetActivesPastEndDateAsync(DateTime utcNow)
        {
            return await _dbContext.Set<PlanSubscription>()
                .Where(s => s.Status == Core.Enums.Plan.PlanSubscriptionStatusEnum.Active && s.EndDate < utcNow)
                .ToListAsync();
        }
    }

    public interface IPlanSubscriptionRepository : IGenericRepository<PlanSubscription>
    {
        Task<PagedResult<PlanSubscription>> GetSubscribersPaged(int planId, int page, int pageSize);

        Task<PlanSubscription?> GetActiveByCompanyAsync(int companyId);
        Task<List<PlanSubscription>> GetByCompanyAsync(int companyId);
        Task<PlanSubscription?> GetByStripeSubscriptionIdAsync(string stripeSubscriptionId);
        
        Task<List<PlanSubscription>> GetAllByStripeSubscriptionIdAsync(string stripeSubscriptionId);
Task<List<PlanSubscription>> GetActivesPastEndDateAsync(DateTime utcNow);
    }
}
