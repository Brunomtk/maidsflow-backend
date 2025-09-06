namespace Core.DTO.Review
{
    public class ReviewFiltersDTO
    {
        public string? Status { get; set; }      // e.g. "pending", "published", "rejected", or "all"
        public string? Rating { get; set; }      // e.g. "1", "2", ..., "5", or "all"
        public string? SearchQuery { get; set; }

        public string? CustomerId { get; set; }
        public string? ProfessionalId { get; set; }
        public string? TeamId { get; set; }
        public string? CompanyId { get; set; }
        public string? AppointmentId { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
