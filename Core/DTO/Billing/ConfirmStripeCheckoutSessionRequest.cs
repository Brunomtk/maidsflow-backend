namespace Core.DTO.Billing
{
    public class ConfirmStripeCheckoutSessionRequest
    {
        /// <summary>
        /// ID da Checkout Session retornado pelo Stripe (ex.: cs_test_...)
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Para o caso do plano FREE (ou trial), permite forçar a duração em dias.
        /// Se null, o serviço decide automaticamente (FREE => 15 dias).
        /// </summary>
        public int? ForceTrialDays { get; set; }
    }
}
