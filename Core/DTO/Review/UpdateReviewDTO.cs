// Core/DTO/Review/UpdateReviewDTO.cs
using System;
using Core.Enums;

namespace Core.DTO.Review
{
    public class UpdateReviewDTO
    {
        public string? CustomerId { get; set; }
        public string? CustomerName { get; set; }

        public string? ProfessionalId { get; set; }
        public string? ProfessionalName { get; set; }

        public string? TeamId { get; set; }
        public string? TeamName { get; set; }

        public string? CompanyId { get; set; }
        public string? CompanyName { get; set; }

        public string? AppointmentId { get; set; }

        public int? Rating { get; set; }
        public string? Comment { get; set; }

        public DateTime? Date { get; set; }

        public string? ServiceType { get; set; }

        public ReviewStatus? Status { get; set; }

        public string? Response { get; set; }
        public DateTime? ResponseDate { get; set; }
    }
}
