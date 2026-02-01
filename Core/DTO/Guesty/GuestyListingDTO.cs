namespace Core.DTO.Guesty
{
    public class GuestyListingDTO
    {
        public string Id { get; set; } = string.Empty;
        public string? Nickname { get; set; }
        public string? Title { get; set; }

        /// <summary>
        /// Stable display name used across Guesty UI and sync.
        /// Prefer Nickname, fallback to Title, and finally to "Guesty {Id}".
        /// </summary>
        public string? DisplayName { get; set; }

        // Location / address fields (used to sync into CustomerAddresses)
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? ZipCode { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }

        // Coordinates (optional)
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        public string? PictureUrl { get; set; }
        public string? Status { get; set; }
    }
}
