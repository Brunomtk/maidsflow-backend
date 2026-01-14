using Core.DTO.Billing;

namespace Services.Integrations.Stripe
{
    public interface IStripeBillingService
    {
        Task<List<StripePriceDTO>> ListActiveRecurringPricesAsync();
        Task<CreateStripeCheckoutSessionResponse> CreateCheckoutSessionAsync(CreateStripeCheckoutSessionRequest request);
        Task HandleWebhookAsync(string json, string stripeSignatureHeader);

        /// <summary>
        /// Confirma uma Checkout Session retornada pelo Stripe (fallback para casos onde o webhook atrasa/falha).
        /// Se o pagamento estiver completo, ativa o plano correspondente para a Company.
        /// </summary>
        Task<ConfirmStripeCheckoutSessionResponse> ConfirmCheckoutSessionAsync(ConfirmStripeCheckoutSessionRequest request);
    }
}
