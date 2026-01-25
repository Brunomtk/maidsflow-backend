using System;
using Core.Enums;

namespace Core.DTO.Review
{
    public class CreateReviewDTO
    {
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }

        public int? ProfessionalId { get; set; }
        public string? ProfessionalName { get; set; }

        public int? TeamId { get; set; }
        public string? TeamName { get; set; }

        public int CompanyId { get; set; }
        public string? CompanyName { get; set; }

        public int AppointmentId { get; set; }


        public int? CustomerAddressId { get; set; }

        /// <summary>
        /// Public identifier used to generate a shareable review link.
        /// If omitted, the backend will generate one.
        /// </summary>
        public Guid? PublicToken { get; set; }

        public int Rating { get; set; }
        public string? Comment { get; set; }

        public DateTime Date { get; set; } = DateTime.UtcNow;

        public string ServiceType { get; set; } = string.Empty;

        public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

        public string? Response { get; set; }
        public DateTime? ResponseDate { get; set; }
    }
}
