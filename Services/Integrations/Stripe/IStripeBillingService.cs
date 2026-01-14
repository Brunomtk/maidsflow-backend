using Core.DTO.Billing;

namespace Services.Integrations.Stripe
{
    public interface IStripeBillingService
    {
        Task<List<StripePriceDTO>> ListActiveRecurringPricesAsync();
        Task<CreateStripeCheckoutSessionResponse> CreateCheckoutSessionAsync(CreateStripeCheckoutSessionRequest request);
        Task HandleWebhookAsync(string json, string stripeSignatureHeader);
    }
}
