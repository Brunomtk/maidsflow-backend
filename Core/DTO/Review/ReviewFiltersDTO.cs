namespace Core.DTO.Review
{
    public class ReviewFiltersDTO
    {
        public string? Status { get; set; }      // e.g. "pending", "published", "rejected", or "all"
        public string? Rating { get; set; }      // e.g. "1", "2", ..., "5", or "all"
        public string? SearchQuery { get; set; }

        public int? CustomerId { get; set; }
        public int? ProfessionalId { get; set; }
        public int? TeamId { get; set; }
        public int? CompanyId { get; set; }
        public int? AppointmentId { get; set; }

        public int? CustomerAddressId { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
