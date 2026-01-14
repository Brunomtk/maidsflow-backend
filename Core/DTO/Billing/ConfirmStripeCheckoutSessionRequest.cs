namespace Core.DTO.Billing
{
    public class ConfirmStripeCheckoutSessionRequest
    {
        // Stripe Checkout Session Id (cs_...)
        public required string SessionId { get; set; }

        // Apenas admin pode informar. Para Company/Professional, o backend ignora e usa o companyId do token.
        public int? CompanyId { get; set; }
    }
}
