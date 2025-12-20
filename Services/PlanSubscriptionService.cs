using Core.Models;
using Core.Enums.Plan;
using System;
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
            await RefreshExpiredSubscriptionsAsync();
            return await _unitOfWork.PlanSubscriptions.GetSubscribersPaged(planId, page, pageSize);
        }

        public async Task<PlanSubscription?> GetActiveByCompanyAsync(int companyId)
        {
            await RefreshExpiredSubscriptionsAsync();
            return await _unitOfWork.PlanSubscriptions.GetActiveByCompanyAsync(companyId);
        }

        public async Task<List<PlanSubscription>> GetByCompanyAsync(int companyId)
        {
            await RefreshExpiredSubscriptionsAsync();
            return await _unitOfWork.PlanSubscriptions.GetByCompanyAsync(companyId);
        }

                private async Task DeactivateOtherActivesAsync(int companyId, int? exceptSubscriptionId = null)
        {
            var subs = await _unitOfWork.PlanSubscriptions.GetByCompanyAsync(companyId);

            foreach (var s in subs)
            {
                if (s.Status == PlanSubscriptionStatusEnum.Active && (!exceptSubscriptionId.HasValue || s.Id != exceptSubscriptionId.Value))
                {
                    s.Status = PlanSubscriptionStatusEnum.Inactive;
                    _unitOfWork.PlanSubscriptions.Update(s);
                }
            }
        }

        /// <summary>
        /// Ativa um plano para uma Company (cria uma nova assinatura Active e desativa qualquer outra Active).
        /// OBS: Plan.Duration é em DIAS (não meses).
        /// </summary>
        public async Task<PlanSubscription> ActivateAsync(int planId, int companyId, bool autoRenew)
            => await ActivateAsync(planId, companyId, autoRenew, null, null);

        /// <summary>
        /// Ativa um plano para uma Company com datas opcionais.
        /// Se EndDateUtc não for informado, ele calcula StartDateUtc + Duration(dias).
        /// </summary>
        public async Task<PlanSubscription> ActivateAsync(int planId, int companyId, bool autoRenew, DateTime? startDateUtc, DateTime? endDateUtc)
        {
            // validações básicas
            var plan = await _unitOfWork.Plans.GetById(planId);
            if (plan == null) throw new InvalidOperationException("Plano não encontrado.");

            var company = await _unitOfWork.Companies.GetById(companyId);
            if (company == null) throw new InvalidOperationException("Company não encontrada.");

            await RefreshExpiredSubscriptionsAsync();

            // desativa TODAS as ativas (caso já exista bug com múltiplas Active)
            await DeactivateOtherActivesAsync(companyId);

            var start = (startDateUtc ?? DateTime.UtcNow).ToUniversalTime();

            // Duration agora é DIAS
            var durationDays = plan.Duration <= 0 ? 30 : plan.Duration;
            var end = (endDateUtc?.ToUniversalTime()) ?? start.AddDays(durationDays);

            if (end < start) throw new InvalidOperationException("EndDateUtc não pode ser menor que StartDateUtc.");

            var subscription = new PlanSubscription
            {
                PlanId = planId,
                CompanyId = companyId,
                StartDate = start,
                EndDate = end,
                Status = PlanSubscriptionStatusEnum.Active,
                AutoRenew = autoRenew
            };

            await _unitOfWork.PlanSubscriptions.Add(subscription);

            // Mantém Company.PlanId alinhado com a assinatura ativa
            company.PlanId = planId;
            _unitOfWork.Companies.Update(company);

            _unitOfWork.Save();
            return subscription;
        }

        /// <summary>
        /// Ativa uma assinatura existente (Status = Active), desativando outras da mesma Company.
        /// Pode opcionalmente ajustar datas e AutoRenew.
        /// </summary>
        public async Task<PlanSubscription?> ActivateExistingAsync(int subscriptionId, DateTime? startDateUtc, DateTime? endDateUtc, bool? autoRenew)
        {
            var subscription = await _unitOfWork.PlanSubscriptions.GetById(subscriptionId);
            if (subscription == null) return null;

            var plan = await _unitOfWork.Plans.GetById(subscription.PlanId);
            if (plan == null) throw new InvalidOperationException("Plano não encontrado.");

            await RefreshExpiredSubscriptionsAsync();

            await DeactivateOtherActivesAsync(subscription.CompanyId, subscriptionId);

            if (autoRenew.HasValue)
                subscription.AutoRenew = autoRenew.Value;

            if (startDateUtc.HasValue)
                subscription.StartDate = startDateUtc.Value.ToUniversalTime();

            // Se enviou start mas não enviou end, recalcula end usando Duration(dias)
            if (endDateUtc.HasValue)
            {
                subscription.EndDate = endDateUtc.Value.ToUniversalTime();
            }
            else if (startDateUtc.HasValue)
            {
                var durationDays = plan.Duration <= 0 ? 30 : plan.Duration;
                subscription.EndDate = subscription.StartDate.AddDays(durationDays);
            }

            if (subscription.EndDate < subscription.StartDate)
                throw new InvalidOperationException("EndDateUtc não pode ser menor que StartDateUtc.");

            subscription.Status = PlanSubscriptionStatusEnum.Active;
            _unitOfWork.PlanSubscriptions.Update(subscription);

            var company = await _unitOfWork.Companies.GetById(subscription.CompanyId);
            if (company != null)
            {
                company.PlanId = subscription.PlanId;
                _unitOfWork.Companies.Update(company);
            }

            _unitOfWork.Save();
            return subscription;
        }

        /// <summary>
        /// Atualiza uma assinatura: StartDate / EndDate / AutoRenew / Status.
        /// Se Status for Active, garante somente 1 Active por Company.
        /// </summary>
        public async Task<PlanSubscription?> UpdateAsync(int subscriptionId, DateTime? startDateUtc, DateTime? endDateUtc, bool? autoRenew, PlanSubscriptionStatusEnum? status)
        {
            var subscription = await _unitOfWork.PlanSubscriptions.GetById(subscriptionId);
            if (subscription == null) return null;

            var plan = await _unitOfWork.Plans.GetById(subscription.PlanId);
            if (plan == null) throw new InvalidOperationException("Plano não encontrado.");

            await RefreshExpiredSubscriptionsAsync();

            if (autoRenew.HasValue)
                subscription.AutoRenew = autoRenew.Value;

            if (startDateUtc.HasValue)
                subscription.StartDate = startDateUtc.Value.ToUniversalTime();

            if (endDateUtc.HasValue)
                subscription.EndDate = endDateUtc.Value.ToUniversalTime();

            // Se ficou inválido, tenta recalcular EndDate baseado na Duration(dias)
            if (subscription.EndDate < subscription.StartDate)
            {
                var durationDays = plan.Duration <= 0 ? 30 : plan.Duration;
                subscription.EndDate = subscription.StartDate.AddDays(durationDays);
            }

            if (status.HasValue)
            {
                if (status.Value == PlanSubscriptionStatusEnum.Active)
                    await DeactivateOtherActivesAsync(subscription.CompanyId, subscriptionId);

                subscription.Status = status.Value;
            }

            _unitOfWork.PlanSubscriptions.Update(subscription);

            // Se ficou Active, alinha Company.PlanId
            if (subscription.Status == PlanSubscriptionStatusEnum.Active)
            {
                var company = await _unitOfWork.Companies.GetById(subscription.CompanyId);
                if (company != null)
                {
                    company.PlanId = subscription.PlanId;
                    _unitOfWork.Companies.Update(company);
                }
            }

            _unitOfWork.Save();
            return subscription;
        }

        /// <summary>
        /// Troca o plano de uma Company (cria nova assinatura Active e desativa qualquer outra Active).
        /// </summary>
        public async Task<PlanSubscription> SwitchPlanAsync(int companyId, int newPlanId, bool autoRenew, DateTime? startDateUtc, DateTime? endDateUtc)
            => await ActivateAsync(newPlanId, companyId, autoRenew, startDateUtc, endDateUtc);

public async Task<bool> CancelAsync(int subscriptionId)
        {
            var subscription = await _unitOfWork.PlanSubscriptions.GetById(subscriptionId);
            if (subscription == null) return false;

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

        // Cria uma nova assinatura Active (desativando qualquer outra Active da mesma Company)
        Task<PlanSubscription> ActivateAsync(int planId, int companyId, bool autoRenew);
        Task<PlanSubscription> ActivateAsync(int planId, int companyId, bool autoRenew, DateTime? startDateUtc, DateTime? endDateUtc);

        // Ativa uma assinatura já existente (por Id)
        Task<PlanSubscription?> ActivateExistingAsync(int subscriptionId, DateTime? startDateUtc, DateTime? endDateUtc, bool? autoRenew);

        // Atualiza StartDate/EndDate/AutoRenew/Status (Status pode ser Cancelled)
        Task<PlanSubscription?> UpdateAsync(int subscriptionId, DateTime? startDateUtc, DateTime? endDateUtc, bool? autoRenew, PlanSubscriptionStatusEnum? status);

        // Troca plano da company (cria nova assinatura Active)
        Task<PlanSubscription> SwitchPlanAsync(int companyId, int newPlanId, bool autoRenew, DateTime? startDateUtc, DateTime? endDateUtc);

        // Cancela assinatura (Status = Cancelled)
        Task<bool> CancelAsync(int subscriptionId);

        Task<int> RefreshExpiredSubscriptionsAsync();
    }
}