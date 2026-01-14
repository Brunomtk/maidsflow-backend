namespace Core.Options
{
    public class StripeOptions
    {
        public const string SectionName = "Stripe";

        // Secret key (sk_...) - NEVER expose to frontend
        public string SecretKey { get; set; } = string.Empty;

        // Webhook signing secret (whsec_...) used to validate Stripe webhook signatures
        public string WebhookSecret { get; set; } = string.Empty;

        // Optional: currency for checkout (default: usd)
        public string Currency { get; set; } = "usd";
    }
}
