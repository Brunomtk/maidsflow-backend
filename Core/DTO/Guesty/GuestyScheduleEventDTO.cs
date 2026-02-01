namespace Core.DTO.Guesty
{
    public class GuestyScheduleEventDTO
    {
        public string Id { get; set; } = string.Empty;
        public string ListingId { get; set; } = string.Empty;

        /// <summary>
        /// Listing display title (same rule used in listings + sync).
        /// This is what should appear in the calendar UI.
        /// </summary>
        public string? ListingTitle { get; set; }

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

        /// <summary>
        /// Best-effort reservation id extracted from Guesty calendar payload.
        /// This is the same id expected by CreateFromGuestyAsync (ExternalReservationId).
        /// </summary>
        public string? GuestyReservationId { get; set; }

        /// <summary>
        /// Resolved entities from our DB (based on sync CustomerAddresses by GuestyListingId).
        /// Helps the frontend prefill and also allows the UI to "mark" events reliably.
        /// </summary>
        public int? CustomerId { get; set; }
        public int? CustomerAddressId { get; set; }

        /// <summary>
        /// If an appointment already exists in MaidsFlow for this reservation,
        /// we attach it here so the frontend can show "already created".
        /// </summary>
        public int? LinkedAppointmentId { get; set; }
    }
}
