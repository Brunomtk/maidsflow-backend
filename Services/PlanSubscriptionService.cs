using Core.Models;
using System;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Exceptions;
using Services.Security;

namespace Services
{
    public class PlanSubscriptionService : IPlanSubscriptionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public PlanSubscriptionService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IScopeGuard scope)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<PagedResult<PlanSubscription>> GetSubscribersByPlan(int planId, int page, int pageSize)
        {
            if (!_currentUser.IsAdmin)
                throw new ForbiddenException("Somente admin pode listar assinantes por plano.");

            await RefreshExpiredSubscriptionsAsync();
            return await _unitOfWork.PlanSubscriptions.GetSubscribersPaged(planId, page, pageSize);
        }

        public async Task<PlanSubscription?> GetActiveByCompanyAsync(int companyId)
        {
            await RefreshExpiredSubscriptionsAsync();

            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company inválido.");
                companyId = scopedCompanyId.Value;
            }

            return await _unitOfWork.PlanSubscriptions.GetActiveByCompanyAsync(companyId);
        }

        public async Task<List<PlanSubscription>> GetByCompanyAsync(int companyId)
        {
            await RefreshExpiredSubscriptionsAsync();

            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company inválido.");
                companyId = scopedCompanyId.Value;
            }

            return await _unitOfWork.PlanSubscriptions.GetByCompanyAsync(companyId);
        }

        public async Task<PlanSubscription> ActivateAsync(int planId, int companyId, bool autoRenew)
        {
            // Company can activate only for itself; Professional can't.
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não pode alterar o plano.");

            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company inválido.");
                companyId = scopedCompanyId.Value;
            }

            // validações básicas
            var plan = await _unitOfWork.Plans.GetById(planId);
            if (plan == null) throw new InvalidOperationException("Plano não encontrado.");

            var company = await _unitOfWork.Companies.GetById(companyId);
            if (company == null) throw new InvalidOperationException("Company não encontrada.");

            await RefreshExpiredSubscriptionsAsync();

            // desativa assinatura ativa anterior
            var currentActive = await _unitOfWork.PlanSubscriptions.GetActiveByCompanyAsync(companyId);
            if (currentActive != null)
            {
                currentActive.Status = Core.Enums.Plan.PlanSubscriptionStatusEnum.Inactive;
                _unitOfWork.PlanSubscriptions.Update(currentActive);
            }

            // Duration in this project is treated as DAYS (e.g. 15 days trial / 30 days monthly).
            // Older comment in Plan.cs mentioned months, but production usage is days.
            var start = DateTime.UtcNow;
            var durationDays = plan.Duration <= 0 ? 1 : plan.Duration;
            var end = start.AddDays(durationDays);

            var subscription = new PlanSubscription
            {
                PlanId = planId,
                CompanyId = companyId,
                StartDate = start,
                EndDate = end,
                Status = Core.Enums.Plan.PlanSubscriptionStatusEnum.Active,
                AutoRenew = autoRenew
            };

            await _unitOfWork.PlanSubscriptions.Add(subscription);
            _unitOfWork.Save();

            return subscription;
        }

        public async Task<PlanSubscription> ActivateTrial15DaysAsync(int planId, int companyId)
        {
            // Company can activate only for itself; Professional can't.
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não pode alterar o plano.");

            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company inválido.");
                companyId = scopedCompanyId.Value;
            }

            var plan = await _unitOfWork.Plans.GetById(planId);
            if (plan == null) throw new InvalidOperationException("Plano não encontrado.");
            if (plan.Status != Core.Enums.StatusEnum.Active)
                throw new InvalidOperationException("Plano está inativo.");

            var company = await _unitOfWork.Companies.GetById(companyId);
            if (company == null) throw new InvalidOperationException("Company não encontrada.");

            await RefreshExpiredSubscriptionsAsync();

            // desativa assinatura ativa anterior
            var currentActive = await _unitOfWork.PlanSubscriptions.GetActiveByCompanyAsync(companyId);
            if (currentActive != null)
            {
                currentActive.Status = Core.Enums.Plan.PlanSubscriptionStatusEnum.Inactive;
                // encerra imediatamente para evitar sobreposição
                currentActive.EndDate = DateTime.UtcNow;
                _unitOfWork.PlanSubscriptions.Update(currentActive);
            }

            var start = DateTime.UtcNow;
            var end = start.AddDays(15);

            var subscription = new PlanSubscription
            {
                PlanId = planId,
                CompanyId = companyId,
                StartDate = start,
                EndDate = end,
                Status = Core.Enums.Plan.PlanSubscriptionStatusEnum.Active,
                AutoRenew = false
            };

            await _unitOfWork.PlanSubscriptions.Add(subscription);
            _unitOfWork.Save();

            return subscription;
        }

        public async Task<bool> CancelAsync(int subscriptionId)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não pode cancelar assinatura.");

            var subscription = await _unitOfWork.PlanSubscriptions.GetById(subscriptionId);
            if (subscription == null) return false;

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(subscription.CompanyId);

            subscription.Status = Core.Enums.Plan.PlanSubscriptionStatusEnum.Cancelled;
            _unitOfWork.PlanSubscriptions.Update(subscription);
            return _unitOfWork.Save() > 0;
        }

        public async Task<int> RefreshExpiredSubscriptionsAsync()
        {
            var now = DateTime.UtcNow;
            var expired = await _unitOfWork.PlanSubscriptions.GetActivesPastEndDateAsync(now);
            if (expired.Count == 0) return 0;

            foreach (var sub in expired)
            {
                sub.Status = Core.Enums.Plan.PlanSubscriptionStatusEnum.Expired;
                _unitOfWork.PlanSubscriptions.Update(sub);
            }

            return _unitOfWork.Save();
        }
    }

    public interface IPlanSubscriptionService
    {
        Task<PagedResult<PlanSubscription>> GetSubscribersByPlan(int planId, int page, int pageSize);
        Task<PlanSubscription?> GetActiveByCompanyAsync(int companyId);
        Task<List<PlanSubscription>> GetByCompanyAsync(int companyId);
        Task<PlanSubscription> ActivateAsync(int planId, int companyId, bool autoRenew);
        Task<PlanSubscription> ActivateTrial15DaysAsync(int planId, int companyId);
        Task<bool> CancelAsync(int subscriptionId);
        Task<int> RefreshExpiredSubscriptionsAsync();
    }
}
