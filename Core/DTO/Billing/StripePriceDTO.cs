namespace Core.DTO.Billing
{
    public class StripePriceDTO
    {
        public required string PriceId { get; set; }
        public string? ProductId { get; set; }
        public string? ProductName { get; set; }
        public long UnitAmount { get; set; }
        public string Currency { get; set; } = "usd";
        public string? Interval { get; set; } // month/year
        public bool Active { get; set; }
    }
}
