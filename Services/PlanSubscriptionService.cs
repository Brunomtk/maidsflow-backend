using Core.Models;
using System;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Exceptions;
using Core.Options;
using Microsoft.Extensions.Options;
using Services.Security;

namespace Services
{
    public class PlanSubscriptionService : IPlanSubscriptionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;
        private readonly StripeOptions _stripeOpts;

        public PlanSubscriptionService(
            IUnitOfWork unitOfWork,
            ICurrentUser currentUser,
            IScopeGuard scope,
            IOptions<StripeOptions> stripeOpts)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
            _stripeOpts = stripeOpts.Value;

            if (!string.IsNullOrWhiteSpace(_stripeOpts.SecretKey))
                global::Stripe.StripeConfiguration.ApiKey = _stripeOpts.SecretKey;
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

        /// <summary>
        /// Ativa um plano para uma empresa.
        /// 
        /// Regras:
        /// - Admin pode ativar manualmente (ex.: planos gratuitos/testes/ativação assistida).
        /// - Company NÃO pode “ativar por conta própria” um plano pago do Stripe sem pagamento confirmado.
        ///   Para planos vinculados ao Stripe (StripePriceId preenchido e Price > 0), a ativação só ocorre
        ///   se existir uma assinatura no Stripe para o customer da empresa com status ativo (ou trialing).
        /// 
        /// Duração: Plan.Duration é tratado como DIAS (ex.: 30, 15).
        /// </summary>
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
            if (plan == null) throw new BadRequestException("Plano não encontrado.");

            var company = await _unitOfWork.Companies.GetById(companyId);
            if (company == null) throw new BadRequestException("Company não encontrada.");

            await RefreshExpiredSubscriptionsAsync();

            // Planos pagos vinculados ao Stripe: company só ativa com pagamento confirmado.
            var isStripeBoundPaidPlan = plan.Price > 0 && !string.IsNullOrWhiteSpace(plan.StripePriceId);
            if (!_currentUser.IsAdmin && isStripeBoundPaidPlan)
            {
                return await ActivateFromStripeAsync(plan, company);
            }

            // Ativação manual (admin ou plano gratuito/não-Stripe)
            await DeactivateCurrentActiveIfAnyAsync(companyId);

            var start = DateTime.UtcNow;
            var durationDays = plan.Duration <= 0 ? 30 : plan.Duration; // padrão: 30 dias
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

            // Sempre manter o vínculo da company com o plano vigente
            company.PlanId = planId;
            _unitOfWork.Companies.Update(company);

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
            if (plan == null) throw new BadRequestException("Plano não encontrado.");
            if (plan.Status != Core.Enums.StatusEnum.Active)
                throw new BadRequestException("Plano está inativo.");

            var company = await _unitOfWork.Companies.GetById(companyId);
            if (company == null) throw new BadRequestException("Company não encontrada.");

            await RefreshExpiredSubscriptionsAsync();

            // desativa assinatura ativa anterior
            await DeactivateCurrentActiveIfAnyAsync(companyId);

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

            company.PlanId = planId;
            _unitOfWork.Companies.Update(company);

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

            // Tenta cancelar no Stripe (se existir vínculo), mas não deixa o cancel local falhar.
            if (!string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId) && !string.IsNullOrWhiteSpace(_stripeOpts.SecretKey))
            {
                try
                {
                    var stripeService = new global::Stripe.SubscriptionService();
                    await stripeService.CancelAsync(subscription.StripeSubscriptionId);
                }
                catch
                {
                    // ignore: cancel local continua valendo
                }
            }

            subscription.Status = Core.Enums.Plan.PlanSubscriptionStatusEnum.Cancelled;
            subscription.AutoRenew = false;
            subscription.EndDate = DateTime.UtcNow;
            _unitOfWork.PlanSubscriptions.Update(subscription);

            await UpdateCompanyPlanIdFromSubscriptionsAsync(subscription.CompanyId);

            return _unitOfWork.Save() > 0;
        }

        public async Task<int> RefreshExpiredSubscriptionsAsync()
        {
            var now = DateTime.UtcNow;
            var expired = await _unitOfWork.PlanSubscriptions.GetActivesPastEndDateAsync(now);
            if (expired.Count == 0) return 0;

            var affectedCompanyIds = new HashSet<int>();

            foreach (var sub in expired)
            {
                sub.Status = Core.Enums.Plan.PlanSubscriptionStatusEnum.Expired;
                sub.AutoRenew = false;
                _unitOfWork.PlanSubscriptions.Update(sub);
                affectedCompanyIds.Add(sub.CompanyId);
            }

            var changed = _unitOfWork.Save();

            // Mantém Company.PlanId consistente (se não houver ativa, limpa)
            foreach (var companyId in affectedCompanyIds)
                await UpdateCompanyPlanIdFromSubscriptionsAsync(companyId);

            _unitOfWork.Save();

            return changed;
        }

        private async Task DeactivateCurrentActiveIfAnyAsync(int companyId)
        {
            var currentActive = await _unitOfWork.PlanSubscriptions.GetActiveByCompanyAsync(companyId);
            if (currentActive == null) return;

            currentActive.Status = Core.Enums.Plan.PlanSubscriptionStatusEnum.Inactive;
            currentActive.AutoRenew = false;
            currentActive.EndDate = DateTime.UtcNow;
            _unitOfWork.PlanSubscriptions.Update(currentActive);
        }

        private async Task UpdateCompanyPlanIdFromSubscriptionsAsync(int companyId)
        {
            var company = await _unitOfWork.Companies.GetById(companyId);
            if (company == null) return;

            // tenta achar ativa (o RefreshExpiredSubscriptionsAsync garante que EndDate < now não fica Active)
            var active = await _unitOfWork.PlanSubscriptions.GetActiveByCompanyAsync(companyId);
            company.PlanId = active?.PlanId;
            _unitOfWork.Companies.Update(company);
        }

        private void EnsureStripeConfigured()
        {
            if (string.IsNullOrWhiteSpace(_stripeOpts.SecretKey))
                throw new BadRequestException("Stripe não configurado (SecretKey ausente). ");
        }

        private async Task<PlanSubscription> ActivateFromStripeAsync(Plan plan, Company company)
        {
            EnsureStripeConfigured();

            if (string.IsNullOrWhiteSpace(company.StripeCustomerId))
                throw new BadRequestException("Empresa sem StripeCustomerId. Crie o checkout e conclua o pagamento para ativar.");

            // Busca subscriptions do customer e tenta achar uma ativa (ou trialing) com o mesmo priceId
            var subscriptionService = new global::Stripe.SubscriptionService();
            var list = await subscriptionService.ListAsync(new global::Stripe.SubscriptionListOptions
            {
                Customer = company.StripeCustomerId,
                Limit = 20,
                Expand = new List<string> { "data.items.data.price" }
            });

            var targetPriceId = plan.StripePriceId!.Trim();
            var now = DateTime.UtcNow;

            var match = list?.Data
                ?.Where(s => s != null)
                ?.Where(s => HasPrice(s, targetPriceId))
                ?.OrderByDescending(s => s.Created)
                ?.FirstOrDefault(s =>
                {
                    var status = (s.Status ?? string.Empty).ToLowerInvariant();
                    if (status != "active" && status != "trialing")
                        return false;

                    // Stripe.CurrentPeriodEnd pode ser DateTime (não-nullable) dependendo da versão do SDK.
                    // Nesse caso, EnsureUtc(DateTime) retorna DateTime e não existe HasValue/Value.
                    var periodEnd = EnsureUtc(s.CurrentPeriodEnd);
                    return periodEnd > now;
                });

            if (match == null)
                throw new BadRequestException("Pagamento não confirmado no Stripe. O plano só será ativado após a assinatura ficar ativa.");

            var stripeSubId = match.Id;
            var periodStart = EnsureUtc((DateTime?)match.CurrentPeriodStart) ?? DateTime.UtcNow;
            var periodEnd = EnsureUtc((DateTime?)match.CurrentPeriodEnd) ?? DateTime.UtcNow;
            var autoRenew = !match.CancelAtPeriodEnd;

            // idempotência: se já existe, atualiza
            var local = await _unitOfWork.PlanSubscriptions.GetByStripeSubscriptionIdAsync(stripeSubId);

            if (local == null)
            {
                // desativa assinatura ativa anterior
                await DeactivateCurrentActiveIfAnyAsync(company.Id);

                local = new PlanSubscription
                {
                    PlanId = plan.Id,
                    CompanyId = company.Id,
                    StartDate = periodStart,
                    EndDate = periodEnd,
                    Status = Core.Enums.Plan.PlanSubscriptionStatusEnum.Active,
                    AutoRenew = autoRenew,
                    StripeSubscriptionId = stripeSubId
                };

                company.PlanId = plan.Id;
                _unitOfWork.Companies.Update(company);
                await _unitOfWork.PlanSubscriptions.Add(local);
                _unitOfWork.Save();
                return local;
            }

            // Troca de plano / atualização de datas
            if (local.PlanId != plan.Id)
                local.PlanId = plan.Id;

            local.StartDate = periodStart;
            local.EndDate = periodEnd;
            local.Status = Core.Enums.Plan.PlanSubscriptionStatusEnum.Active;
            local.AutoRenew = autoRenew;

            company.PlanId = plan.Id;
            _unitOfWork.Companies.Update(company);
            _unitOfWork.PlanSubscriptions.Update(local);
            _unitOfWork.Save();

            return local;
        }

        private static bool HasPrice(global::Stripe.Subscription s, string priceId)
        {
            try
            {
                var items = s.Items?.Data;
                if (items == null) return false;
                foreach (var it in items)
                {
                    var p = it?.Price;
                    if (p != null && string.Equals(p.Id, priceId, StringComparison.Ordinal))
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static DateTime? EnsureUtc(DateTime? dt)
        {
            if (!dt.HasValue) return null;
            return DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc);
        }

        private static DateTime EnsureUtc(DateTime dt)
        {
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
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
