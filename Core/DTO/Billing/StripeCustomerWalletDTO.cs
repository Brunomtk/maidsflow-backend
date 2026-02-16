namespace Core.DTO.Billing
{
    public class StripeCustomerWalletDTO
    {
        public string? CustomerId { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }

        /// <summary>
        /// Customer balance in the smallest currency unit (ex.: cents). In Stripe, positive means the customer owes money;
        /// negative means the customer has credit.
        /// </summary>
        public long AccountBalance { get; set; }

        public string? Currency { get; set; }
    }
}
