namespace Core.DTO.Billing
{
    public class CreateStripeCheckoutSessionResponse
    {
        public required string CheckoutUrl { get; set; }
        public required string SessionId { get; set; }
    }
}
