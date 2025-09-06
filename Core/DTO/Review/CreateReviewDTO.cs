using System;
using Core.Enums;

namespace Core.DTO.Review
{
    public class CreateReviewDTO
    {
        public string CustomerId { get; set; } = string.Empty;
        public string? CustomerName { get; set; }

        public string? ProfessionalId { get; set; }
        public string? ProfessionalName { get; set; }

        public string? TeamId { get; set; }
        public string? TeamName { get; set; }

        public string CompanyId { get; set; } = string.Empty;
        public string? CompanyName { get; set; }

        public string AppointmentId { get; set; } = string.Empty;

        public int Rating { get; set; }
        public string? Comment { get; set; }

        public DateTime Date { get; set; } = DateTime.UtcNow;

        public string ServiceType { get; set; } = string.Empty;

        public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

        public string? Response { get; set; }
        public DateTime? ResponseDate { get; set; }
    }
}
