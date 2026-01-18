using Core.DTO.Billing;

namespace Services.Integrations.Stripe
{
    public interface IStripeBillingService
    {
        Task<List<StripePriceDTO>> ListActiveRecurringPricesAsync();
        Task<CreateStripeCheckoutSessionResponse> CreateCheckoutSessionAsync(CreateStripeCheckoutSessionRequest request);
        /// <summary>
        /// Confirma uma Checkout Session (client-to-server) e garante o registro local em PlanSubscriptions
        /// com StripeSubscriptionId preenchido.
        /// Útil quando o front precisa "confirmar e ativar" imediatamente após o redirect do Stripe.
        /// </summary>
        Task ConfirmCheckoutSessionAsync(ConfirmStripeCheckoutSessionRequest request);
        /// <summary>
        /// Retorna o histórico de cobranças (invoices) no Stripe para a company do usuário logado.
        /// Admin pode informar CompanyId via query.
        /// </summary>
        Task<List<BillingHistoryItemDTO>> GetBillingHistoryAsync(int? companyId = null, int limit = 20);
        Task HandleWebhookAsync(string json, string stripeSignatureHeader);
    }
}
