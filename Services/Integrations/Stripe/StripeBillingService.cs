using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Core.DTO.Billing;
using Core.Exceptions;
using Core.Options;
using Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using Services.Security;

namespace Services.Integrations.Stripe
{
    public class StripeBillingService : IStripeBillingService
    {
        private readonly StripeOptions _opts;
        private readonly IUnitOfWork _uow;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public StripeBillingService(IOptions<StripeOptions> opts, IUnitOfWork uow, ICurrentUser currentUser, IScopeGuard scope)
        {
            _opts = opts.Value;
            _uow = uow;
            _currentUser = currentUser;
            _scope = scope;

            if (!string.IsNullOrWhiteSpace(_opts.SecretKey))
                global::Stripe.StripeConfiguration.ApiKey = _opts.SecretKey;
        }

        public async Task<List<StripePriceDTO>> ListActiveRecurringPricesAsync()
        {
            EnsureStripeConfigured();

            var priceService = new global::Stripe.PriceService();
            var options = new global::Stripe.PriceListOptions
            {
                Active = true,
                Limit = 100,
                Expand = new List<string> { "data.product" }
            };

            var prices = await priceService.ListAsync(options);

            return prices
                .Where(p => p.Recurring != null)
                .Select(p => new StripePriceDTO
                {
                    PriceId = p.Id,
                    ProductId = (p.Product as global::Stripe.Product)?.Id ?? p.ProductId,
                    ProductName = (p.Product as global::Stripe.Product)?.Name,
                    UnitAmount = p.UnitAmount ?? 0,
                    Currency = p.Currency ?? _opts.Currency,
                    Interval = p.Recurring?.Interval,
                    Active = p.Active
                })
                .OrderBy(p => p.UnitAmount)
                .ToList();
        }

        public async Task<CreateStripeCheckoutSessionResponse> CreateCheckoutSessionAsync(CreateStripeCheckoutSessionRequest request)
        {
            EnsureStripeConfigured();

            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.PriceId)) throw new InvalidOperationException("PriceId Ã© obrigatÃ³rio.");
            if (string.IsNullOrWhiteSpace(request.SuccessUrl) || string.IsNullOrWhiteSpace(request.CancelUrl))
                throw new InvalidOperationException("SuccessUrl e CancelUrl sÃ£o obrigatÃ³rios.");

            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional nÃ£o pode assinar/alterar planos.");

            int companyId;
            if (_currentUser.IsAdmin)
            {
                if (!request.CompanyId.HasValue || request.CompanyId.Value <= 0)
                    throw new InvalidOperationException("CompanyId Ã© obrigatÃ³rio para admin.");
                companyId = request.CompanyId.Value;
            }
            else
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company invÃ¡lido.");
                companyId = scopedCompanyId.Value;
            }

            // garante que o PriceId estÃ¡ vinculado a um Plan do sistema
            var plan = await _uow.Plans.GetByStripePriceIdAsync(request.PriceId);
            if (plan == null)
                throw new InvalidOperationException("Este PriceId nÃ£o estÃ¡ vinculado a nenhum plano do sistema.");

            var company = await _uow.Companies.GetById(companyId);
            if (company == null)
                throw new InvalidOperationException("Company nÃ£o encontrada.");

            // Create/reuse Stripe customer (nÃ£o confundir com Services.CustomerService)
            var customerId = company.StripeCustomerId;
            if (string.IsNullOrWhiteSpace(customerId))
            {
                var stripeCustomerService = new global::Stripe.CustomerService();
                var createdCustomer = await stripeCustomerService.CreateAsync(new global::Stripe.CustomerCreateOptions
                {
                    Name = company.Name,
                    Email = company.Email,
                    Phone = company.Phone,
                    Metadata = new Dictionary<string, string>
                    {
                        ["companyId"] = company.Id.ToString()
                    }
                });

                customerId = createdCustomer.Id;
                company.StripeCustomerId = customerId;
                _uow.Companies.Update(company);
                _uow.Save();
            }

            var successUrl = EnsureSessionIdPlaceholder(request.SuccessUrl);

            var sessionService = new global::Stripe.Checkout.SessionService();
            var session = await sessionService.CreateAsync(new global::Stripe.Checkout.SessionCreateOptions
            {
                Mode = "subscription",
                Customer = customerId,
                SuccessUrl = successUrl,
                CancelUrl = request.CancelUrl,
                LineItems = new List<global::Stripe.Checkout.SessionLineItemOptions>
                {
                    new global::Stripe.Checkout.SessionLineItemOptions
                    {
                        Price = request.PriceId,
                        Quantity = 1
                    }
                },
                Metadata = new Dictionary<string, string>
                {
                    ["companyId"] = companyId.ToString(),
                    ["priceId"] = request.PriceId,
                    ["planId"] = plan.Id.ToString()
                }
            });

            return new CreateStripeCheckoutSessionResponse
            {
                CheckoutUrl = session.Url,
                SessionId = session.Id
            };
        }

        public async Task<ConfirmStripeCheckoutSessionResponse> ConfirmCheckoutSessionAsync(ConfirmStripeCheckoutSessionRequest request)
        {
            EnsureStripeConfigured();

            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.SessionId))
                throw new InvalidOperationException("SessionId é obrigatório.");

            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não pode assinar/alterar planos.");

            var sessionService = new global::Stripe.Checkout.SessionService();
            var session = await sessionService.GetAsync(request.SessionId);

            var isPaid = string.Equals(session?.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(session?.Status, "complete", StringComparison.OrdinalIgnoreCase);

            if (!isPaid)
            {
                return new ConfirmStripeCheckoutSessionResponse
                {
                    Activated = false
                };
            }

            // Resolve CompanyId (metadata > admin request > token scope > StripeCustomerId)
            int companyId = 0;
            var metaCompanyId = TryParseIntFromMetadata(session?.Metadata, "companyId");
            if (metaCompanyId.HasValue && metaCompanyId.Value > 0)
                companyId = metaCompanyId.Value;

            if (companyId <= 0 && _currentUser.IsAdmin)
            {
                if (request.CompanyId.HasValue && request.CompanyId.Value > 0)
                    companyId = request.CompanyId.Value;
            }

            if (companyId <= 0 && !_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (scopedCompanyId.HasValue && scopedCompanyId.Value > 0)
                    companyId = scopedCompanyId.Value;
            }

            if (companyId <= 0)
            {
                // fallback: tentar por StripeCustomerId
                var custId = session?.CustomerId ?? TryGetStringProperty(session, "Customer");
                if (!string.IsNullOrWhiteSpace(custId))
                {
                    var companyByCustomer = await _uow.Companies.GetByStripeCustomerIdAsync(custId);
                    if (companyByCustomer != null)
                        companyId = companyByCustomer.Id;
                }
            }

            if (companyId <= 0)
                throw new InvalidOperationException("Não foi possível resolver CompanyId para confirmar o checkout.");

            // subscriptionId (se for modo subscription)
            var subscriptionId = TryGetCheckoutSessionSubscriptionId(session);

            // customerId (para persistir em Companies.StripeCustomerId se precisar)
            var sessionCustomerId = session?.CustomerId ?? TryGetStringProperty(session, "Customer");

            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                // caminho ideal: sincroniza a assinatura baseada no Stripe Subscription
                var subscriptionService = new global::Stripe.SubscriptionService();
                var stripeSub = await subscriptionService.GetAsync(subscriptionId, new global::Stripe.SubscriptionGetOptions
                {
                    Expand = new List<string> { "items.data.price" }
                });

                await SyncSubscriptionFromStripeAsync(stripeSub, deletedEvent: false);

                var local = await _uow.PlanSubscriptions.GetByStripeSubscriptionIdAsync(subscriptionId);
                return new ConfirmStripeCheckoutSessionResponse
                {
                    Activated = local != null && local.Status == Core.Enums.Plan.PlanSubscriptionStatusEnum.Active,
                    CompanyId = companyId,
                    PlanId = local?.PlanId,
                    StripeSubscriptionId = subscriptionId
                };
            }

            // fallback: sem subscriptionId, tenta resolver priceId e ativar via plano local
            var priceId = TryGetMetadataValue(session?.Metadata, "priceId");
            if (string.IsNullOrWhiteSpace(priceId))
            {
                // Tenta obter via line items
                try
                {
                    var lineItems = await sessionService.ListLineItemsAsync(request.SessionId, new global::Stripe.Checkout.SessionListLineItemsOptions
                    {
                        Limit = 1,
                        Expand = new List<string> { "data.price" }
                    });

                    priceId = lineItems?.Data?.FirstOrDefault()?.Price?.Id;
                }
                catch
                {
                    // ignora e deixa a validação abaixo disparar
                }
            }

            if (string.IsNullOrWhiteSpace(priceId))
                throw new InvalidOperationException("Não foi possível resolver priceId da sessão.");

            // garante que o PriceId está vinculado a um Plan do sistema
            var plan = await _uow.Plans.GetByStripePriceIdAsync(priceId);
            if (plan == null)
                throw new InvalidOperationException("Este priceId não está vinculado a nenhum plano do sistema.");

            var company = await _uow.Companies.GetById(companyId);
            if (company == null)
                throw new InvalidOperationException("Company não encontrada.");

            if (!string.IsNullOrWhiteSpace(sessionCustomerId) && string.IsNullOrWhiteSpace(company.StripeCustomerId))
                company.StripeCustomerId = sessionCustomerId;

            // desativa assinatura ativa anterior
            var currentActive = await _uow.PlanSubscriptions.GetActiveByCompanyAsync(companyId);
            if (currentActive != null)
            {
                currentActive.Status = Core.Enums.Plan.PlanSubscriptionStatusEnum.Inactive;
                currentActive.EndDate = DateTime.UtcNow;
                _uow.PlanSubscriptions.Update(currentActive);
            }

            var start = DateTime.UtcNow;
            var months = plan.Duration <= 0 ? 1 : plan.Duration;
            var end = start.AddMonths(months);

            var sub = new Core.Models.PlanSubscription
            {
                PlanId = plan.Id,
                CompanyId = companyId,
                StartDate = start,
                EndDate = end,
                Status = Core.Enums.Plan.PlanSubscriptionStatusEnum.Active,
                AutoRenew = true,
                StripeSubscriptionId = null
            };

            company.PlanId = plan.Id;
            _uow.Companies.Update(company);
            await _uow.PlanSubscriptions.Add(sub);

            _uow.Save();

            return new ConfirmStripeCheckoutSessionResponse
            {
                Activated = true,
                CompanyId = companyId,
                PlanId = plan.Id,
                StripeSubscriptionId = subscriptionId
            };
        }

        public async Task HandleWebhookAsync(string json, string stripeSignatureHeader)
        {
            EnsureStripeConfigured();

            if (string.IsNullOrWhiteSpace(_opts.WebhookSecret))
                throw new InvalidOperationException("Stripe:WebhookSecret nÃ£o configurado.");

            global::Stripe.Event stripeEvent;
            try
            {
                stripeEvent = global::Stripe.EventUtility.ConstructEvent(json, stripeSignatureHeader, _opts.WebhookSecret);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Assinatura do webhook invÃ¡lida.", ex);
            }

            // 1) Checkout finalizado (cria/ativa assinatura local)
            if (stripeEvent.Type == global::Stripe.Events.CheckoutSessionCompleted)
            {
                await HandleCheckoutSessionCompletedAsync(json);
                return;
            }

            // 2) MantÃ©m o status sincronizado com a Stripe (cancelamento / troca / renovaÃ§Ã£o)
            if (stripeEvent.Type == global::Stripe.Events.CustomerSubscriptionUpdated)
            {
                var stripeSub = stripeEvent.Data.Object as global::Stripe.Subscription;
                if (stripeSub != null)
                    await SyncSubscriptionFromStripeAsync(stripeSub, deletedEvent: false);
                return;
            }

            if (stripeEvent.Type == global::Stripe.Events.CustomerSubscriptionDeleted)
            {
                var stripeSub = stripeEvent.Data.Object as global::Stripe.Subscription;
                if (stripeSub != null)
                    await SyncSubscriptionFromStripeAsync(stripeSub, deletedEvent: true);
                return;
            }

            // Ignora eventos que nÃ£o nos interessam
            return;
        }

        private async Task HandleCheckoutSessionCompletedAsync(string json)
        {
            var payload = ExtractCheckoutCompletedPayload(json);
            if (payload.CompanyId <= 0)
                throw new InvalidOperationException("Webhook sem companyId na metadata.");

            // Se for subscription, usamos o perÃ­odo oficial do Stripe (current_period_start/end)
            StripeSubscriptionSnapshot? stripeSub = null;
            if (!string.IsNullOrWhiteSpace(payload.SubscriptionId))
            {
                stripeSub = await TryGetSubscriptionSnapshotAsync(payload.SubscriptionId);

                // idempotÃªncia por StripeSubscriptionId
                var existing = await _uow.PlanSubscriptions.GetByStripeSubscriptionIdAsync(payload.SubscriptionId);
                if (existing != null)
                    return;
            }

            var effectivePriceId = stripeSub?.PriceId ?? payload.PriceId;
            if (string.IsNullOrWhiteSpace(effectivePriceId))
                throw new InvalidOperationException("Webhook sem priceId (metadata e subscription vazios).");

            var plan = await _uow.Plans.GetByStripePriceIdAsync(effectivePriceId);
            if (plan == null)
                throw new InvalidOperationException("Webhook: priceId nÃ£o vinculado a nenhum plano do sistema.");

            var company = await _uow.Companies.GetById(payload.CompanyId);
            if (company == null)
                throw new InvalidOperationException("Webhook: company nÃ£o encontrada.");

            // Persist customer id if needed
            if (!string.IsNullOrWhiteSpace(payload.CustomerId) && string.IsNullOrWhiteSpace(company.StripeCustomerId))
                company.StripeCustomerId = payload.CustomerId;

            // desativa subscription ativa anterior
            var currentActive = await _uow.PlanSubscriptions.GetActiveByCompanyAsync(payload.CompanyId);
            if (currentActive != null)
            {
                currentActive.Status = Core.Enums.Plan.PlanSubscriptionStatusEnum.Inactive;
                currentActive.EndDate = DateTime.UtcNow;
                _uow.PlanSubscriptions.Update(currentActive);
            }

            DateTime start;
            DateTime end;
            bool autoRenew;

            if (stripeSub != null && stripeSub.PeriodStartUtc.HasValue && stripeSub.PeriodEndUtc.HasValue)
            {
                start = stripeSub.PeriodStartUtc.Value;
                end = stripeSub.PeriodEndUtc.Value;
                autoRenew = stripeSub.AutoRenew;
            }
            else
            {
                // fallback (nÃ£o ideal): usa Duration do plano local
                start = DateTime.UtcNow;
                var months = plan.Duration <= 0 ? 1 : plan.Duration;
                end = start.AddMonths(months);
                autoRenew = true;
            }

            var sub = new Core.Models.PlanSubscription
            {
                PlanId = plan.Id,
                CompanyId = payload.CompanyId,
                StartDate = start,
                EndDate = end,
                Status = Core.Enums.Plan.PlanSubscriptionStatusEnum.Active,
                AutoRenew = autoRenew,
                StripeSubscriptionId = payload.SubscriptionId
            };

            company.PlanId = plan.Id;
            _uow.Companies.Update(company);
            await _uow.PlanSubscriptions.Add(sub);

            _uow.Save();
        }

        private async Task SyncSubscriptionFromStripeAsync(global::Stripe.Subscription stripeSub, bool deletedEvent)
        {
            // Resolve company
            var companyId = await ResolveCompanyIdFromSubscriptionAsync(stripeSub);
            if (companyId <= 0)
                return; // sem empresa identificÃ¡vel, nÃ£o quebrar webhook

            var company = await _uow.Companies.GetById(companyId);
            if (company == null)
                return;

            // Garantir StripeCustomerId armazenado
            if (!string.IsNullOrWhiteSpace(stripeSub.CustomerId) && string.IsNullOrWhiteSpace(company.StripeCustomerId))
                company.StripeCustomerId = stripeSub.CustomerId;

            var priceId = ExtractPriceIdFromSubscription(stripeSub);
            var plan = !string.IsNullOrWhiteSpace(priceId)
                ? await _uow.Plans.GetByStripePriceIdAsync(priceId)
                : null;

            // Datas oficiais
            var periodStart = EnsureUtc(stripeSub.CurrentPeriodStart);
            var periodEnd = EnsureUtc(stripeSub.CurrentPeriodEnd);

            // Status local
            var localStatus = MapStripeStatusToLocal(stripeSub.Status, periodEnd, deletedEvent);
            var autoRenew = !stripeSub.CancelAtPeriodEnd;

            // Local subscription por StripeSubscriptionId
            var localSub = await _uow.PlanSubscriptions.GetByStripeSubscriptionIdAsync(stripeSub.Id);

            if (localSub == null)
            {
                // Se por algum motivo nÃ£o tivemos checkout.session.completed (webhook perdido), criamos o registro
                if (plan == null) return;

                // desativa assinatura ativa anterior se esta vier como Active
                if (localStatus == Core.Enums.Plan.PlanSubscriptionStatusEnum.Active)
                {
                    var currentActive = await _uow.PlanSubscriptions.GetActiveByCompanyAsync(companyId);
                    if (currentActive != null)
                    {
                        currentActive.Status = Core.Enums.Plan.PlanSubscriptionStatusEnum.Inactive;
                        currentActive.EndDate = DateTime.UtcNow;
                        _uow.PlanSubscriptions.Update(currentActive);
                    }
                }

                var newSub = new Core.Models.PlanSubscription
                {
                    PlanId = plan.Id,
                    CompanyId = companyId,
                    StartDate = periodStart ?? DateTime.UtcNow,
                    EndDate = periodEnd ?? DateTime.UtcNow,
                    Status = localStatus,
                    AutoRenew = autoRenew,
                    StripeSubscriptionId = stripeSub.Id
                };

                if (localStatus == Core.Enums.Plan.PlanSubscriptionStatusEnum.Active)
                    company.PlanId = plan.Id;

                _uow.Companies.Update(company);
                await _uow.PlanSubscriptions.Add(newSub);
                _uow.Save();
                return;
            }

            // Atualiza a assinatura existente
            if (plan != null && localSub.PlanId != plan.Id)
            {
                localSub.PlanId = plan.Id;
                // Troca de plano: atualizar Company.PlanId tambÃ©m
                company.PlanId = plan.Id;
            }

            if (periodStart.HasValue)
                localSub.StartDate = periodStart.Value;
            if (periodEnd.HasValue)
                localSub.EndDate = periodEnd.Value;

            localSub.AutoRenew = autoRenew;
            localSub.Status = localStatus;

            // Se esta assinatura estÃ¡ ativa, inativa qualquer outra ativa (garante 1 ativa por company)
            if (localStatus == Core.Enums.Plan.PlanSubscriptionStatusEnum.Active)
            {
                var currentActive = await _uow.PlanSubscriptions.GetActiveByCompanyAsync(companyId);
                if (currentActive != null && currentActive.Id != localSub.Id)
                {
                    currentActive.Status = Core.Enums.Plan.PlanSubscriptionStatusEnum.Inactive;
                    currentActive.EndDate = DateTime.UtcNow;
                    _uow.PlanSubscriptions.Update(currentActive);
                }
            }

            _uow.PlanSubscriptions.Update(localSub);
            _uow.Companies.Update(company);
            _uow.Save();
        }

        private async Task<int> ResolveCompanyIdFromSubscriptionAsync(global::Stripe.Subscription stripeSub)
        {
            // 1) metadata.companyId (melhor caminho)
            if (stripeSub.Metadata != null && stripeSub.Metadata.TryGetValue("companyId", out var companyIdStr))
            {
                if (int.TryParse(companyIdStr, out var companyId) && companyId > 0)
                    return companyId;
            }

            // 2) procurar por StripeCustomerId
            if (!string.IsNullOrWhiteSpace(stripeSub.CustomerId))
            {
                var company = await _uow.Companies.GetByStripeCustomerIdAsync(stripeSub.CustomerId);
                if (company != null) return company.Id;
            }

            return 0;
        }

        private static string? ExtractPriceIdFromSubscription(global::Stripe.Subscription stripeSub)
        {
            try
            {
                var item = stripeSub.Items?.Data?.FirstOrDefault();
				// Stripe.NET (versões recentes): SubscriptionItem.Price.Id
				if (item?.Price != null && !string.IsNullOrWhiteSpace(item.Price.Id))
					return item.Price.Id;

				// Compatibilidade defensiva entre versões: tentar via reflection (PriceId/Plan.Id)
				if (item == null) return null;
				var t = item.GetType();

				var priceIdProp = t.GetProperty("PriceId");
				if (priceIdProp != null)
				{
					var v = priceIdProp.GetValue(item) as string;
					if (!string.IsNullOrWhiteSpace(v)) return v;
				}

				var planProp = t.GetProperty("Plan");
				if (planProp != null)
				{
					var planObj = planProp.GetValue(item);
					if (planObj != null)
					{
						var idProp = planObj.GetType().GetProperty("Id");
						var id = idProp?.GetValue(planObj) as string;
						if (!string.IsNullOrWhiteSpace(id)) return id;
					}
				}

				return null;
            }
            catch
            {
                return null;
            }
        }

        private static DateTime? EnsureUtc(DateTime? dt)
        {
            if (!dt.HasValue) return null;
            return DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc);
        }

        private static Core.Enums.Plan.PlanSubscriptionStatusEnum MapStripeStatusToLocal(string? stripeStatus, DateTime? periodEndUtc, bool deletedEvent)
        {
            var now = DateTime.UtcNow;

            if (periodEndUtc.HasValue && periodEndUtc.Value < now)
                return Core.Enums.Plan.PlanSubscriptionStatusEnum.Expired;

            if (deletedEvent)
                return Core.Enums.Plan.PlanSubscriptionStatusEnum.Cancelled;

            var s = (stripeStatus ?? string.Empty).ToLowerInvariant();
            return s switch
            {
                "active" => Core.Enums.Plan.PlanSubscriptionStatusEnum.Active,
                "trialing" => Core.Enums.Plan.PlanSubscriptionStatusEnum.Active,
                "canceled" => Core.Enums.Plan.PlanSubscriptionStatusEnum.Cancelled,
                "incomplete_expired" => Core.Enums.Plan.PlanSubscriptionStatusEnum.Expired,
                "past_due" => Core.Enums.Plan.PlanSubscriptionStatusEnum.Inactive,
                "unpaid" => Core.Enums.Plan.PlanSubscriptionStatusEnum.Inactive,
                "incomplete" => Core.Enums.Plan.PlanSubscriptionStatusEnum.Inactive,
                _ => Core.Enums.Plan.PlanSubscriptionStatusEnum.Inactive
            };
        }

        private void EnsureStripeConfigured()
        {
            if (string.IsNullOrWhiteSpace(_opts.SecretKey))
                throw new InvalidOperationException("Stripe:SecretKey nÃ£o configurado.");
        }

        private static int? TryParseIntFromMetadata(Dictionary<string, string>? metadata, string key)
        {
            if (metadata == null) return null;
            if (!metadata.TryGetValue(key, out var raw)) return null;
            return int.TryParse(raw, out var v) ? v : null;
        }

        private static string? TryGetMetadataValue(Dictionary<string, string>? metadata, string key)
        {
            if (metadata == null) return null;
            return metadata.TryGetValue(key, out var v) ? v : null;
        }

        private static string? TryGetStringProperty(object? obj, string propName)
        {
            if (obj == null) return null;
            var prop = obj.GetType().GetProperty(propName);
            if (prop == null) return null;
            return prop.GetValue(obj) as string;
        }

        private static string? TryGetCheckoutSessionSubscriptionId(global::Stripe.Checkout.Session? session)
        {
            if (session == null) return null;

            // Stripe.NET versions may expose SubscriptionId directly
            var direct = session.SubscriptionId;
            if (!string.IsNullOrWhiteSpace(direct)) return direct;

            // defensivo entre versões: tentar via reflection (Subscription/SubscriptionId)
            var t = session.GetType();

            var subIdProp = t.GetProperty("SubscriptionId");
            if (subIdProp != null)
            {
                var v = subIdProp.GetValue(session) as string;
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }

            var subProp = t.GetProperty("Subscription");
            if (subProp != null)
            {
                var subObj = subProp.GetValue(session);
                var idProp = subObj?.GetType().GetProperty("Id");
                var v = idProp?.GetValue(subObj) as string;
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }

            return null;
        }

        private static CheckoutCompletedPayload ExtractCheckoutCompletedPayload(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var dataObj = doc.RootElement.GetProperty("data").GetProperty("object");

            int companyId = 0;
            string? priceId = null;
            string? customerId = null;
            string? subscriptionId = null;

            if (dataObj.TryGetProperty("metadata", out var meta) && meta.ValueKind == JsonValueKind.Object)
            {
                if (meta.TryGetProperty("companyId", out var c))
                    int.TryParse(c.GetString(), out companyId);

                if (meta.TryGetProperty("priceId", out var p))
                    priceId = p.GetString();
            }

            if (dataObj.TryGetProperty("customer", out var cust))
                customerId = cust.GetString();

            if (dataObj.TryGetProperty("subscription", out var sub))
                subscriptionId = sub.GetString();

            return new CheckoutCompletedPayload(companyId, priceId, customerId, subscriptionId);
        }

        private static string EnsureSessionIdPlaceholder(string successUrl)
        {
            if (string.IsNullOrWhiteSpace(successUrl)) return successUrl;
            if (successUrl.Contains("{CHECKOUT_SESSION_ID}", StringComparison.Ordinal)) return successUrl;

            var separator = successUrl.Contains("?", StringComparison.Ordinal) ? "&" : "?";
            return successUrl + separator + "session_id={CHECKOUT_SESSION_ID}";
        }

        private async Task<StripeSubscriptionSnapshot?> TryGetSubscriptionSnapshotAsync(string subscriptionId)
        {
            try
            {
                var subscriptionService = new global::Stripe.SubscriptionService();
                var stripeSub = await subscriptionService.GetAsync(subscriptionId, new global::Stripe.SubscriptionGetOptions
                {
                    Expand = new List<string> { "items.data.price" }
                });

                var item = stripeSub.Items?.Data?.FirstOrDefault();
                var priceId = item?.Price?.Id;

                // Stripe.net geralmente jÃ¡ converte timestamps para DateTime (UTC). Se vier null/default, ignoramos.
                DateTime? periodStart = stripeSub.CurrentPeriodStart;
                DateTime? periodEnd = stripeSub.CurrentPeriodEnd;

                if (periodStart.HasValue)
                    periodStart = DateTime.SpecifyKind(periodStart.Value, DateTimeKind.Utc);
                if (periodEnd.HasValue)
                    periodEnd = DateTime.SpecifyKind(periodEnd.Value, DateTimeKind.Utc);

                var cancelAtPeriodEnd = stripeSub.CancelAtPeriodEnd;

                return new StripeSubscriptionSnapshot
                {
                    SubscriptionId = stripeSub.Id,
                    PriceId = priceId,
                    PeriodStartUtc = periodStart,
                    PeriodEndUtc = periodEnd,
                    AutoRenew = !(cancelAtPeriodEnd is true)
                };
            }
            catch
            {
                return null;
            }
        }

        private record CheckoutCompletedPayload(int CompanyId, string? PriceId, string? CustomerId, string? SubscriptionId);

        private class StripeSubscriptionSnapshot
        {
            public string? SubscriptionId { get; set; }
            public string? PriceId { get; set; }
            public DateTime? PeriodStartUtc { get; set; }
            public DateTime? PeriodEndUtc { get; set; }
            public bool AutoRenew { get; set; }
        }
    }
}
