using System;
using System.Collections.Generic;

namespace Core.DTO.Guesty
{
    public class GuestySyncCustomerAddressesResultDTO
    {
        public int CustomerId { get; set; }
        public bool DryRun { get; set; }
        public int ListingsSeen { get; set; }
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

        public List<GuestyListingAddressLinkDTO> Links { get; set; } = new();
    }

    public class GuestyListingAddressLinkDTO
    {
        public string ListingId { get; set; } = string.Empty;
        public string? ListingTitle { get; set; }
        public int? CustomerAddressId { get; set; }
        public string Action { get; set; } = "skipped"; // created|updated|skipped
        public string? Reason { get; set; }
    }
}
