using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;

namespace Services
{
    public class PlanSubscriptionService : IPlanSubscriptionService
    {
        private readonly Infrastructure.Repositories.IUnitOfWork _unitOfWork;

        public PlanSubscriptionService(Infrastructure.Repositories.IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<PagedResult<PlanSubscription>> GetSubscribersByPlan(int planId, int page, int pageSize)
        {
            return await _unitOfWork.PlanSubscriptions.GetSubscribersPaged(planId, page, pageSize);
        }
    }

    public interface IPlanSubscriptionService
    {
        Task<PagedResult<PlanSubscription>> GetSubscribersByPlan(int planId, int page, int pageSize);
    }
}
