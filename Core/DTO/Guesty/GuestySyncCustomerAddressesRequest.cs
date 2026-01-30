namespace Core.DTO.Guesty
{
    public class GuestySyncCustomerAddressesRequest
    {
        public int CustomerId { get; set; }
        public bool DryRun { get; set; }

        public bool UpdateExisting { get; set; } = true;
        public bool MatchByAddress { get; set; } = true;
        public bool SetPrimaryIfNone { get; set; } = true;
        public bool OnlyActiveListings { get; set; } = true;
        public int Limit { get; set; } = 200;
    }
}
