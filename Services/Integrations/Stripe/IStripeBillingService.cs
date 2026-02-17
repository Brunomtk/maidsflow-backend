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

        /// <summary>
        /// Retorna um resumo do Stripe para a empresa (últimas assinaturas + saldo do customer + próxima cobrança).
        /// Company: usa a company do token.
        /// Admin: pode informar companyId.
        /// </summary>
        Task<StripeBillingSummaryDTO> GetStripeBillingSummaryAsync(int? companyId = null, int subscriptionsLimit = 10);

        /// <summary>
        /// Sincroniza datas/status de assinaturas locais com o Stripe.
        /// - Company: sincroniza apenas a própria empresa.
        /// - Admin: pode sincronizar uma empresa específica (companyId) ou todas (syncAll=true).
        /// Objetivo: manter StartDate/EndDate/Status/AutoRenew sempre alinhados ao Stripe.
        /// </summary>
        Task<StripeDatesSyncResultDTO> SyncStripeDatesAsync(int? companyId = null, bool syncAll = false);

        Task HandleWebhookAsync(string json, string stripeSignatureHeader);
    }
}
