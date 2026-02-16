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
using Services.Email;
using Services.Security;

namespace Services.Integrations.Stripe
{
    public class StripeBillingService : IStripeBillingService
    {
        private readonly StripeOptions _opts;
        private readonly IUnitOfWork _uow;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;
        private readonly IPlanPaymentEmailService _planEmail;

        public StripeBillingService(IOptions<StripeOptions> opts, IUnitOfWork uow, ICurrentUser currentUser, IScopeGuard scope, IPlanPaymentEmailService planEmail)
        {
            _opts = opts.Value;
            _uow = uow;
            _currentUser = currentUser;
            _scope = scope;
            _planEmail = planEmail;

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

        public async Task ConfirmCheckoutSessionAsync(ConfirmStripeCheckoutSessionRequest request)
        {
            EnsureStripeConfigured();

            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.SessionId))
                throw new InvalidOperationException("SessionId é obrigatório.");

            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não pode assinar/alterar planos.");

            // 1) Buscar sessão no Stripe
            var sessionService = new global::Stripe.Checkout.SessionService();
            var session = await sessionService.GetAsync(request.SessionId, new global::Stripe.Checkout.SessionGetOptions
            {
                Expand = new List<string> { "subscription", "customer" }
            });

            // 2) Ler metadata (companyId/priceId/planId)
            var meta = session?.Metadata;
            int companyId = 0;
            string? priceId = null;
            int planId = 0;

            if (meta != null)
            {
                if (meta.TryGetValue("companyId", out var c)) int.TryParse(c, out companyId);
                if (meta.TryGetValue("priceId", out var p)) priceId = p;
                if (meta.TryGetValue("planId", out var pl)) int.TryParse(pl, out planId);
            }

            if (companyId <= 0)
                throw new InvalidOperationException("Checkout Session sem companyId na metadata.");

            // Escopo: company só pode confirmar sua própria sessão
            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue || scopedCompanyId.Value != companyId)
                    throw new ForbiddenException("Escopo de company inválido para confirmar esta sessão.");
            }

            // 3) Descobrir subscriptionId
            var subscriptionId = ReadString(session, "SubscriptionId");
            if (string.IsNullOrWhiteSpace(subscriptionId))
                subscriptionId = ReadString(session, "Subscription") ?? session?.SubscriptionId;

            if (string.IsNullOrWhiteSpace(subscriptionId))
                throw new InvalidOperationException("Checkout Session sem subscriptionId. Verifique se o checkout está em mode=subscription.");

            // 4) Resolve plano
            Core.Models.Plan? plan = null;
            if (planId > 0)
                plan = await _uow.Plans.GetById(planId);

            if (plan == null && !string.IsNullOrWhiteSpace(priceId))
                plan = await _uow.Plans.GetByStripePriceIdAsync(priceId);

            if (plan == null)
                throw new InvalidOperationException("Não foi possível resolver o plano para esta sessão (planId/priceId).");

            var company = await _uow.Companies.GetById(companyId);
            if (company == null)
                throw new InvalidOperationException("Company não encontrada.");

            // Persist customer id if needed
            var customerId = ReadString(session, "CustomerId") ?? ReadString(session, "Customer");
            if (!string.IsNullOrWhiteSpace(customerId) && string.IsNullOrWhiteSpace(company.StripeCustomerId))
                company.StripeCustomerId = customerId;

            // 5) Idempotência por StripeSubscriptionId
            var existingByStripe = await _uow.PlanSubscriptions.GetByStripeSubscriptionIdAsync(subscriptionId);

            DateTime start;
            DateTime end;
            bool autoRenew;

            var isFreePlan = plan.Price <= 0;
            var trialDays = request.ForceTrialDays.HasValue && request.ForceTrialDays.Value > 0
                ? request.ForceTrialDays.Value
                : 15;

            if (isFreePlan)
            {
                // Fluxo: "passou no Stripe" -> ativa trial interno de 15 dias, mas guarda StripeSubscriptionId
                start = DateTime.UtcNow;
                end = start.AddDays(trialDays);
                autoRenew = false;
            }
            else
            {
                // Usa datas oficiais do Stripe
                var snap = await TryGetSubscriptionSnapshotAsync(subscriptionId);
                if (snap?.PeriodStartUtc.HasValue == true && snap?.PeriodEndUtc.HasValue == true)
                {
                    start = snap.PeriodStartUtc.Value;
                    end = snap.PeriodEndUtc.Value;
                    autoRenew = snap.AutoRenew;
                }
                else
                {
                    start = DateTime.UtcNow;
                    var days = plan.Duration <= 0 ? 30 : plan.Duration;
                    end = start.AddDays(days);
                    autoRenew = true;
                }
            }

            // 6) Desativa atual ativa e cria/atualiza local
            var currentActive = await _uow.PlanSubscriptions.GetActiveByCompanyAsync(companyId);
            if (currentActive != null && (existingByStripe == null || currentActive.Id != existingByStripe.Id))
            {
                currentActive.Status = Core.Enums.Plan.PlanSubscriptionStatusEnum.Inactive;
                currentActive.EndDate = DateTime.UtcNow;
                _uow.PlanSubscriptions.Update(currentActive);
            }

            if (existingByStripe == null)
            {
                var newSub = new Core.Models.PlanSubscription
                {
                    PlanId = plan.Id,
                    CompanyId = companyId,
                    StartDate = start,
                    EndDate = end,
                    Status = Core.Enums.Plan.PlanSubscriptionStatusEnum.Active,
                    AutoRenew = autoRenew,
                    StripeSubscriptionId = subscriptionId
                };

                company.PlanId = plan.Id;
                _uow.Companies.Update(company);
                await _uow.PlanSubscriptions.Add(newSub);
                _uow.Save();
                return;
            }

            // Atualiza registro existente (garantindo PlanId e principalmente StripeSubscriptionId)
            existingByStripe.PlanId = plan.Id;
            existingByStripe.CompanyId = companyId;
            existingByStripe.StartDate = start;
            existingByStripe.EndDate = end;
            existingByStripe.AutoRenew = autoRenew;
            existingByStripe.Status = Core.Enums.Plan.PlanSubscriptionStatusEnum.Active;
            existingByStripe.StripeSubscriptionId = subscriptionId;

            company.PlanId = plan.Id;
            _uow.PlanSubscriptions.Update(existingByStripe);
            _uow.Companies.Update(company);
            _uow.Save();
        }

        public async Task<List<BillingHistoryItemDTO>> GetBillingHistoryAsync(int? companyId = null, int limit = 20)
        {
            EnsureStripeConfigured();

            if (limit <= 0) limit = 20;
            if (limit > 100) limit = 100;

            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não pode acessar billing history.");

            int resolvedCompanyId;
            if (_currentUser.IsAdmin)
            {
                if (!companyId.HasValue || companyId.Value <= 0)
                    throw new InvalidOperationException("CompanyId é obrigatório para admin.");
                resolvedCompanyId = companyId.Value;
            }
            else
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company inválido.");
                resolvedCompanyId = scopedCompanyId.Value;
            }

            var company = await _uow.Companies.GetById(resolvedCompanyId);
            if (company == null)
                throw new InvalidOperationException("Company não encontrada.");

            // Se ainda não existe customer no Stripe, não há histórico.
            if (string.IsNullOrWhiteSpace(company.StripeCustomerId))
                return new List<BillingHistoryItemDTO>();

            var invoiceService = new global::Stripe.InvoiceService();
            var invoices = await invoiceService.ListAsync(new global::Stripe.InvoiceListOptions
            {
                Customer = company.StripeCustomerId,
                Limit = limit,
                // ajuda a obter período e links sem chamadas extras
                Expand = new List<string>
                {
                    "data.lines.data.period",
                    "data.status_transitions"
                }
            });

            return invoices
                .Select(inv =>
                {
                    var createdUtc = ReadDateTime(inv, "Created") ?? DateTime.UtcNow;
                    createdUtc = DateTime.SpecifyKind(createdUtc, DateTimeKind.Utc);

                    DateTime? paidAtUtc = null;
                    try
                    {
                        // Stripe.NET: inv.StatusTransitions?.PaidAt pode variar por versão
                        var st = inv.StatusTransitions;
                        if (st != null)
                        {
                            var paidAtProp = st.GetType().GetProperty("PaidAt");
                            if (paidAtProp != null)
                            {
                                var val = paidAtProp.GetValue(st);
                                if (val is DateTime dt)
                                    paidAtUtc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                                else if (val is DateTimeOffset dto)
                                    paidAtUtc = dto.UtcDateTime;
                                else if (val is long l)
                                    paidAtUtc = DateTimeOffset.FromUnixTimeSeconds(l).UtcDateTime;
                                else if (val is int i)
                                    paidAtUtc = DateTimeOffset.FromUnixTimeSeconds(i).UtcDateTime;
                            }
                        }
                    }
                    catch { /* ignorar */ }

                    DateTime? periodStartUtc = null;
                    DateTime? periodEndUtc = null;
                    try
                    {
                        // O período mais confiável em assinaturas vem da primeira line com period
                        var firstLine = inv.Lines?.Data?.FirstOrDefault(l => l.Period != null);
                        if (firstLine?.Period != null)
                        {
                            periodStartUtc = ReadDateTime(firstLine.Period, "Start");
                            periodEndUtc = ReadDateTime(firstLine.Period, "End");
                            if (periodStartUtc.HasValue)
                                periodStartUtc = DateTime.SpecifyKind(periodStartUtc.Value, DateTimeKind.Utc);
                            if (periodEndUtc.HasValue)
                                periodEndUtc = DateTime.SpecifyKind(periodEndUtc.Value, DateTimeKind.Utc);
                        }
                    }
                    catch { /* ignorar */ }

                    return new BillingHistoryItemDTO
                    {
                        InvoiceId = inv.Id,
                        Number = inv.Number,
                        Status = inv.Status,
                        Paid = ReadBool(inv, "Paid") ?? false,
                        AmountDue = ReadLong(inv, "AmountDue") ?? 0,
                        AmountPaid = ReadLong(inv, "AmountPaid") ?? 0,
                        AmountRemaining = ReadLong(inv, "AmountRemaining") ?? 0,
                        Currency = ReadString(inv, "Currency") ?? _opts.Currency,
                        CreatedAtUtc = createdUtc,
                        PaidAtUtc = paidAtUtc,
                        PeriodStartUtc = periodStartUtc,
                        PeriodEndUtc = periodEndUtc,
                        SubscriptionId = inv.SubscriptionId,
                        HostedInvoiceUrl = inv.HostedInvoiceUrl,
                        InvoicePdfUrl = inv.InvoicePdf
                    };
                })
                .OrderByDescending(i => i.CreatedAtUtc)
                .ToList();
        }

        public async Task<StripeBillingSummaryDTO> GetStripeBillingSummaryAsync(int? companyId = null, int subscriptionsLimit = 10)
        {
            EnsureStripeConfigured();

            if (subscriptionsLimit <= 0) subscriptionsLimit = 10;
            if (subscriptionsLimit > 50) subscriptionsLimit = 50;

            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não pode acessar informações de billing.");

            int resolvedCompanyId;
            if (_currentUser.IsAdmin)
            {
                if (!companyId.HasValue || companyId.Value <= 0)
                    throw new InvalidOperationException("CompanyId é obrigatório para admin.");
                resolvedCompanyId = companyId.Value;
            }
            else
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company inválido.");
                resolvedCompanyId = scopedCompanyId.Value;
            }

            var company = await _uow.Companies.GetById(resolvedCompanyId);
            if (company == null)
                throw new InvalidOperationException("Company não encontrada.");

            var summary = new StripeBillingSummaryDTO
            {
                CompanyId = resolvedCompanyId,
                StripeCustomerId = company.StripeCustomerId
            };

            // Se ainda não existe customer no Stripe, não há dados de Stripe para retornar.
            if (string.IsNullOrWhiteSpace(company.StripeCustomerId))
                return summary;

            // 1) Wallet / Customer balance
            var customerService = new global::Stripe.CustomerService();
            var customer = await customerService.GetAsync(company.StripeCustomerId);
            summary.Wallet = new StripeCustomerWalletDTO
            {
                CustomerId = customer.Id,
                Name = customer.Name,
                Email = customer.Email,
                AccountBalance = ReadLong(customer, "Balance") ?? ReadLong(customer, "AccountBalance") ?? 0,
                Currency = customer.Currency ?? _opts.Currency
            };

            // 2) Latest subscriptions
            var subscriptionService = new global::Stripe.SubscriptionService();
            var subs = await subscriptionService.ListAsync(new global::Stripe.SubscriptionListOptions
            {
                Customer = company.StripeCustomerId,
                Limit = subscriptionsLimit,
                Status = "all",
                Expand = new List<string>
                {
                    // Stripe API limita expand a 4 níveis. "data.items.data.price.product" excede.
                    // Expandimos até price e resolvemos o product name via chamada separada (com cache).
                    "data.items.data.price"
                }
            });

            // Cache local para evitar chamadas repetidas no Stripe
            var productNameCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var productService = new global::Stripe.ProductService();

            summary.LatestSubscriptions = subs
                .Select(s =>
                {
                    var item = s.Items?.Data?.FirstOrDefault();
                    var price = item?.Price;

                    // Stripe.net (v45.x): Price.Product é um objeto Product (pode vir apenas com Id quando não expandido)
                    // e o id também pode estar em Price.ProductId. Para evitar limite de expand, buscamos o nome pelo id.
                    string? productName = price?.Product?.Name;
                    var productId = price?.Product?.Id ?? price?.ProductId;

                    if (string.IsNullOrWhiteSpace(productName) && !string.IsNullOrWhiteSpace(productId))
                    {
                        if (!productNameCache.TryGetValue(productId!, out productName))
                        {
                            try
                            {
                                var prod = productService.Get(productId!);
                                productName = prod?.Name;
                            }
                            catch
                            {
                                productName = null;
                            }
                            productNameCache[productId!] = productName;
                        }
                    }

                    var createdAt = ReadDateTime(s, "Created");
                    if (createdAt.HasValue)
                        createdAt = DateTime.SpecifyKind(createdAt.Value, DateTimeKind.Utc);

                    var canceledAt = ReadDateTime(s, "CanceledAt");
                    if (canceledAt.HasValue)
                        canceledAt = DateTime.SpecifyKind(canceledAt.Value, DateTimeKind.Utc);

                    return new StripeSubscriptionInfoDTO
                    {
                        SubscriptionId = s.Id,
                        Status = s.Status,
                        CancelAtPeriodEnd = s.CancelAtPeriodEnd,
                        CurrentPeriodStartUtc = EnsureUtc(s.CurrentPeriodStart),
                        CurrentPeriodEndUtc = EnsureUtc(s.CurrentPeriodEnd),
                        PriceId = price?.Id,
                        ProductName = productName,
                        UnitAmount = price?.UnitAmount ?? 0,
                        Currency = price?.Currency ?? _opts.Currency,
                        Interval = price?.Recurring?.Interval,
                        CreatedAtUtc = createdAt,
                        CanceledAtUtc = canceledAt
                    };
                })
                .OrderByDescending(s => s.CreatedAtUtc ?? DateTime.MinValue)
                .ToList();

            // 3) Próxima cobrança (Upcoming invoice) - tenta pegar da assinatura ativa mais relevante
            var activeSub = summary.LatestSubscriptions
                .FirstOrDefault(s => string.Equals(s.Status, "active", StringComparison.OrdinalIgnoreCase))
                ?? summary.LatestSubscriptions.FirstOrDefault();

            try
            {
                var invoiceService = new global::Stripe.InvoiceService();
                // Stripe.net v45.x usa UpcomingInvoiceOptions (não InvoiceUpcomingOptions)
                var upcoming = await invoiceService.UpcomingAsync(new global::Stripe.UpcomingInvoiceOptions
                {
                    Customer = company.StripeCustomerId,
                    Subscription = activeSub?.SubscriptionId
                });

                if (upcoming != null)
                {
                    DateTime? nextAttempt = null;
                    try
                    {
                        // Stripe.NET pode expor NextPaymentAttempt como Unix/date dependendo da versão
                        nextAttempt = ReadDateTime(upcoming, "NextPaymentAttempt");
                        if (nextAttempt.HasValue)
                            nextAttempt = DateTime.SpecifyKind(nextAttempt.Value, DateTimeKind.Utc);
                    }
                    catch { /* ignore */ }

                    DateTime? periodStartUtc = null;
                    DateTime? periodEndUtc = null;
                    try
                    {
                        var firstLine = upcoming.Lines?.Data?.FirstOrDefault(l => l.Period != null);
                        if (firstLine?.Period != null)
                        {
                            periodStartUtc = ReadDateTime(firstLine.Period, "Start");
                            periodEndUtc = ReadDateTime(firstLine.Period, "End");
                            if (periodStartUtc.HasValue)
                                periodStartUtc = DateTime.SpecifyKind(periodStartUtc.Value, DateTimeKind.Utc);
                            if (periodEndUtc.HasValue)
                                periodEndUtc = DateTime.SpecifyKind(periodEndUtc.Value, DateTimeKind.Utc);
                        }
                    }
                    catch { /* ignore */ }

                    summary.UpcomingCharge = new StripeUpcomingChargeDTO
                    {
                        UpcomingInvoiceId = upcoming.Id,
                        SubscriptionId = activeSub?.SubscriptionId,
                        AmountDue = ReadLong(upcoming, "AmountDue") ?? 0,
                        Currency = ReadString(upcoming, "Currency") ?? _opts.Currency,
                        NextAttemptUtc = nextAttempt,
                        PeriodStartUtc = periodStartUtc,
                        PeriodEndUtc = periodEndUtc
                    };
                }
            }
            catch
            {
                // Upcoming invoice pode falhar em casos específicos (ex.: customer sem invoice próximo);
                // não quebrar o endpoint.
            }

            // Fallback: se não deu pra pegar Upcoming, pelo menos exponha a próxima data via CurrentPeriodEnd.
            if (summary.UpcomingCharge == null && activeSub?.CurrentPeriodEndUtc.HasValue == true)
            {
                summary.UpcomingCharge = new StripeUpcomingChargeDTO
                {
                    UpcomingInvoiceId = null,
                    SubscriptionId = activeSub.SubscriptionId,
                    AmountDue = 0,
                    Currency = activeSub.Currency ?? _opts.Currency,
                    NextAttemptUtc = activeSub.CurrentPeriodEndUtc,
                    PeriodStartUtc = activeSub.CurrentPeriodStartUtc,
                    PeriodEndUtc = activeSub.CurrentPeriodEndUtc
                };
            }

            return summary;
        }

        private static string? ReadString(object obj, string propertyName)
        {
            try
            {
                var prop = obj.GetType().GetProperty(propertyName);
                return prop?.GetValue(obj) as string;
            }
            catch
            {
                return null;
            }
        }

        private static long? ReadLong(object obj, string propertyName)
        {
            try
            {
                var prop = obj.GetType().GetProperty(propertyName);
                var val = prop?.GetValue(obj);
                if (val == null) return null;

                // Nullable primitives, when boxed, become either null or the underlying value type.
                if (val is long l) return l;
                if (val is int i) return i;
                if (val is short s) return s;
                if (val is decimal dec) return (long)dec;
                if (val is double dbl) return (long)dbl;
                if (val is float fl) return (long)fl;
                if (val is string str)
                {
                    if (long.TryParse(str, out var parsed)) return parsed;
                }

                return Convert.ToInt64(val);
            }
            catch
            {
                return null;
            }
        }

        private static bool? ReadBool(object obj, string propertyName)
        {
            try
            {
                var prop = obj.GetType().GetProperty(propertyName);
                var val = prop?.GetValue(obj);
                if (val == null) return null;

                // Nullable bool, when boxed, becomes either null or bool.
                if (val is bool b) return b;
                if (val is string str)
                {
                    if (bool.TryParse(str, out var parsed)) return parsed;
                    if (str == "1") return true;
                    if (str == "0") return false;
                }

                return Convert.ToBoolean(val);
            }
            catch
            {
                return null;
            }
        }

        private static DateTime? ReadDateTime(object obj, string propertyName)
        {
            try
            {
                var prop = obj.GetType().GetProperty(propertyName);
                var val = prop?.GetValue(obj);
                if (val == null) return null;

                // Nullable DateTime, when boxed, becomes either null or DateTime.
                if (val is DateTime dt) return dt;
                if (val is DateTimeOffset dto) return dto.UtcDateTime;

                // Stripe às vezes expõe timestamps como Unix (segundos)
                if (val is long l) return DateTimeOffset.FromUnixTimeSeconds(l).UtcDateTime;
                if (val is int i) return DateTimeOffset.FromUnixTimeSeconds(i).UtcDateTime;

                if (val is string str)
                {
                    if (DateTimeOffset.TryParse(str, out var parsedDto)) return parsedDto.UtcDateTime;
                    if (DateTime.TryParse(str, out var parsedDt)) return DateTime.SpecifyKind(parsedDt, DateTimeKind.Utc);
                    if (long.TryParse(str, out var unix)) return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
                }

                return null;
            }
            catch
            {
                return null;
            }
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

            
            // 3) Pagamento de fatura (renovação/primeiro pagamento) => email de confirmação
            if (stripeEvent.Type == global::Stripe.Events.InvoicePaymentSucceeded || stripeEvent.Type == global::Stripe.Events.InvoicePaid)
            {
                var invoice = stripeEvent.Data.Object as global::Stripe.Invoice;
                if (invoice != null)
                    await HandleInvoicePaidAsync(invoice);
                return;
            }

            // Ignora eventos que nÃ£o nos interessam
            return;
        }

        private async Task HandleInvoicePaidAsync(global::Stripe.Invoice invoice)
        {
            try
            {
                // Resolve Stripe customer id (varies a bit across Stripe.NET versions)
                var customerId = invoice.CustomerId;
                if (string.IsNullOrWhiteSpace(customerId))
                    customerId = ReadString(invoice, "CustomerId");

                if (string.IsNullOrWhiteSpace(customerId) && invoice.Customer is global::Stripe.Customer c)
                    customerId = c.Id;

                if (string.IsNullOrWhiteSpace(customerId))
                    return;

                var company = await _uow.Companies.GetByStripeCustomerIdAsync(customerId);
                if (company == null)
                    return;

                // Amounts come in the smallest currency unit (e.g., cents)
                var amountPaidMinor = ReadLong(invoice, "AmountPaid") ?? ReadLong(invoice, "Total") ?? 0;
                var amountPaid = amountPaidMinor / 100m;

                var currency = ReadString(invoice, "Currency") ?? _opts.Currency;

                var invoiceNumber = ReadString(invoice, "Number") ?? ReadString(invoice, "InvoiceNumber");
                var hostedInvoiceUrl = ReadString(invoice, "HostedInvoiceUrl");
                var invoicePdfUrl = ReadString(invoice, "InvoicePdf");

                long? periodStartUnix = null;
                long? periodEndUnix = null;
                try
                {
                    var firstLine = invoice.Lines?.Data?.FirstOrDefault(l => l.Period != null);
                    if (firstLine?.Period != null)
                    {
                        periodStartUnix = ReadLong(firstLine.Period, "Start");
                        periodEndUnix = ReadLong(firstLine.Period, "End");
                    }
                }
                catch { /* ignore */ }

                long? paidAtUnix = null;
                try
                {
                    var st = invoice.StatusTransitions;
                    if (st != null)
                        paidAtUnix = ReadLong(st, "PaidAt");
                }
                catch { /* ignore */ }

                // Fallback to invoice creation time if paidAt isn't available
                paidAtUnix ??= ReadLong(invoice, "Created");

                await _planEmail.SendPlanPaymentSuccessAsync(
                    companyId: company.Id,
                    amountPaid: amountPaid,
                    currency: currency,
                    invoiceNumber: invoiceNumber,
                    hostedInvoiceUrl: hostedInvoiceUrl,
                    invoicePdfUrl: invoicePdfUrl,
                    periodStartUnix: periodStartUnix,
                    periodEndUnix: periodEndUnix,
                    paidAtUnix: paidAtUnix);
            }
            catch
            {
                // Best-effort: never break the webhook pipeline because of email.
                return;
            }
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
                var days = plan.Duration <= 0 ? 30 : plan.Duration;
                end = start.AddDays(days);
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
            var autoRenew = localStatus == Core.Enums.Plan.PlanSubscriptionStatusEnum.Active && !stripeSub.CancelAtPeriodEnd;

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
                // Só vincula na Company se a assinatura estiver ativa.
                if (localStatus == Core.Enums.Plan.PlanSubscriptionStatusEnum.Active)
                    company.PlanId = plan.Id;
            }

            if (periodStart.HasValue)
                localSub.StartDate = periodStart.Value;
            if (periodEnd.HasValue)
                localSub.EndDate = periodEnd.Value;

            localSub.AutoRenew = autoRenew;
            localSub.Status = localStatus;

            // Sem pagamento/status ativo => não mantém plano vigente nem autorrenovação.
            if (localStatus == Core.Enums.Plan.PlanSubscriptionStatusEnum.Active && plan != null)
                company.PlanId = plan.Id;
            else if (company.PlanId.HasValue && company.PlanId.Value == localSub.PlanId)
                company.PlanId = null;

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
