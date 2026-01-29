namespace Core.Options
{
    public class GuestyOptions
    {
        public const string SectionName = "Guesty";

        // Guesty Booking Engine API base url (no trailing slash)
        public string OpenApiBaseUrl { get; set; } = "https://booking.guesty.com/api";

        // Calendar endpoint is under /listings/{listingId}/calendar.
        // We keep it in one place so it can be overridden if Guesty changes hostnames.
    }
}
