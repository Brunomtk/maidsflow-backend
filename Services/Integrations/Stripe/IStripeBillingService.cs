using Core.DTO.Billing;

namespace Services.Integrations.Stripe
{
    public interface IStripeBillingService
    {
        Task<List<StripePriceDTO>> ListActiveRecurringPricesAsync();
        Task<CreateStripeCheckoutSessionResponse> CreateCheckoutSessionAsync(CreateStripeCheckoutSessionRequest request);
        /// <summary>
        /// Retorna o histórico de cobranças (invoices) no Stripe para a company do usuário logado.
        /// Admin pode informar CompanyId via query.
        /// </summary>
        Task<List<BillingHistoryItemDTO>> GetBillingHistoryAsync(int? companyId = null, int limit = 20);
        Task HandleWebhookAsync(string json, string stripeSignatureHeader);
    }
}
