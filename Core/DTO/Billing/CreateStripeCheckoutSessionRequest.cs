namespace Core.DTO.Billing
{
    public class CreateStripeCheckoutSessionRequest
    {
        // Apenas admin pode informar. Para Company/Professional, o backend ignora e usa o companyId do token.
        public int? CompanyId { get; set; }

        // Stripe Price Id (price_...)
        public required string PriceId { get; set; }

        // URLs de retorno
        public required string SuccessUrl { get; set; }
        public required string CancelUrl { get; set; }
    }
}
