using Core.DTO;
using Core.Enums;
using Infrastructure.ServiceExtension;
using Core.Models;
using Infrastructure.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Exceptions;
using Services.Security;

namespace Services
{
    public class PlanService : IPlanService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPlanSubscriptionService _planSubscriptionService;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public PlanService(IUnitOfWork unitOfWork, IPlanSubscriptionService planSubscriptionService, ICurrentUser currentUser, IScopeGuard scope)
        {
            _unitOfWork = unitOfWork;
            _planSubscriptionService = planSubscriptionService;
            _currentUser = currentUser;
            _scope = scope;
        }

        
        public async Task<PagedResult<Plan>> GetPlansPaged(FiltersDTO filtersDTO)
        {
            await _planSubscriptionService.RefreshExpiredSubscriptionsAsync();

            // Admin can list everything using repository paging
            if (_currentUser.IsAdmin)
                return await _unitOfWork.Plans.GetPlansPaged(filtersDTO);

            // Non-admin: expose only Active plans (needed for plan-expired screen).
            // Because the number of plans is small, we can safely page in memory.
            var plans = await _unitOfWork.Plans.GetAllWithCompaniesAsync();

            var query = plans.AsQueryable().Where(p => p.Status == StatusEnum.Active);

            if (!string.IsNullOrWhiteSpace(filtersDTO.Name))
            {
                var name = filtersDTO.Name.ToLowerInvariant();
                query = query.Where(p =>
                    (!string.IsNullOrWhiteSpace(p.Name) && p.Name.ToLowerInvariant().Contains(name)) ||
                    (p.Features != null && p.Features.Any(f => !string.IsNullOrWhiteSpace(f) && f.ToLowerInvariant().Contains(name)))
                );
            }

            query = query.OrderByDescending(p => p.CreatedDate);

            var pageNumber = filtersDTO.pageNumber <= 0 ? 1 : filtersDTO.pageNumber;
            var pageSize = filtersDTO.pageSize <= 0 ? 10 : filtersDTO.pageSize;

            var totalItems = query.Count();
            var results = query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            var pageCount = (int)System.Math.Ceiling(totalItems / (double)pageSize);

            return new PagedResult<Plan>
            {
                CurrentPage = pageNumber,
                PageSize = pageSize,
                TotalItems = totalItems,
                PageCount = pageCount,
                Results = results
            };
        }


        public async Task<IEnumerable<Plan>> GetAllPlans()
        {
            await _planSubscriptionService.RefreshExpiredSubscriptionsAsync();

            var plans = await _unitOfWork.Plans.GetAllWithCompaniesAsync();

            // For company/professional users: expose only Active plans
            if (!_currentUser.IsAdmin)
                return plans.Where(p => p.Status == StatusEnum.Active);

            return plans;
        }

        public async Task<Plan?> GetPlanById(int id)
        {
            await _planSubscriptionService.RefreshExpiredSubscriptionsAsync();
            var plan = await _unitOfWork.Plans.GetByIdWithCompaniesAsync(id);

            if (plan == null) return null;

            // Non-admins cannot view inactive plans
            if (!_currentUser.IsAdmin && plan.Status != StatusEnum.Active)
                throw new ForbiddenException("Plano inativo.");

            return plan;
        }

        public async Task<bool> CreatePlan(Plan plan)
        {
            if (!_currentUser.IsAdmin)
                throw new ForbiddenException("Somente admin pode criar planos.");

            await _unitOfWork.Plans.Add(plan);
            return _unitOfWork.Save() > 0;
        }

        public async Task<bool> UpdatePlan(Plan plan)
        {
            if (!_currentUser.IsAdmin)
                throw new ForbiddenException("Somente admin pode atualizar planos.");

            var existing = await _unitOfWork.Plans.GetById(plan.Id);
            if (existing == null) return false;

            existing.Name = plan.Name;
            existing.Price = plan.Price;
            existing.Features = plan.Features;
            existing.StripeProductId = plan.StripeProductId;
            existing.StripePriceId = plan.StripePriceId;
            existing.ProfessionalsLimit = plan.ProfessionalsLimit;
            existing.TeamsLimit = plan.TeamsLimit;
            existing.CustomersLimit = plan.CustomersLimit;
            existing.AppointmentsLimit = plan.AppointmentsLimit;
            existing.Duration = plan.Duration;
            existing.Status = plan.Status;

            _unitOfWork.Plans.Update(existing);
            return _unitOfWork.Save() > 0;
        }

        public async Task<bool> DeletePlan(int id)
        {
            if (!_currentUser.IsAdmin)
                throw new ForbiddenException("Somente admin pode deletar planos.");

            var plan = await _unitOfWork.Plans.GetById(id);
            if (plan == null) return false;

            _unitOfWork.Plans.Delete(plan);
            return _unitOfWork.Save() > 0;
        }

        public async Task<bool> UpdateStatus(int id, StatusEnum status)
        {
            if (!_currentUser.IsAdmin)
                throw new ForbiddenException("Somente admin pode alterar status de planos.");

            var plan = await _unitOfWork.Plans.GetById(id);
            if (plan == null) return false;

            plan.Status = status;
            _unitOfWork.Plans.Update(plan);
            return _unitOfWork.Save() > 0;
        }
    }

    public interface IPlanService
    {
        Task<PagedResult<Plan>> GetPlansPaged(FiltersDTO filtersDTO);
        Task<IEnumerable<Plan>> GetAllPlans();
        Task<Plan?> GetPlanById(int id);
        Task<bool> CreatePlan(Plan plan);
        Task<bool> UpdatePlan(Plan plan);
        Task<bool> DeletePlan(int id);
        Task<bool> UpdateStatus(int id, StatusEnum status);
    }
}
