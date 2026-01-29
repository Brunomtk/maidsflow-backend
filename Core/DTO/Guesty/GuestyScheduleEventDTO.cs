namespace Core.DTO.Guesty
{
    public class GuestyScheduleEventDTO
    {
        public string Id { get; set; } = string.Empty;
        public string ListingId { get; set; } = string.Empty;

        // Reservation | Block
        public string Type { get; set; } = "Block";

        // Guesty blockType (b/r/o/m/ic etc.)
        public string? BlockType { get; set; }

        // ISO date strings: YYYY-MM-DD
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;

        public string? Status { get; set; }
        public string? Label { get; set; }
        public string? GuestName { get; set; }
        public string? ConfirmationCode { get; set; }
        public string? Source { get; set; }
    }
}
