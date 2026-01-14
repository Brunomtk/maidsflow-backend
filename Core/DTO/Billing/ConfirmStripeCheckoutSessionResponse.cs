namespace Core.DTO.Billing
{
    public class ConfirmStripeCheckoutSessionResponse
    {
        public bool Activated { get; set; }
        public int? CompanyId { get; set; }
        public int? PlanId { get; set; }
        public string? StripeSubscriptionId { get; set; }
    }
}
