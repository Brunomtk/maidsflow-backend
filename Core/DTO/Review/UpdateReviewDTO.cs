// Core/DTO/Review/UpdateReviewDTO.cs
using System;
using Core.Enums;

namespace Core.DTO.Review
{
    public class UpdateReviewDTO
    {
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }

        public int? ProfessionalId { get; set; }
        public string? ProfessionalName { get; set; }

        public int? TeamId { get; set; }
        public string? TeamName { get; set; }

        public int? CompanyId { get; set; }
        public string? CompanyName { get; set; }

        public int? AppointmentId { get; set; }


        public int? CustomerAddressId { get; set; }

        public Guid? PublicToken { get; set; }

        public int? Rating { get; set; }
        public string? Comment { get; set; }

        public DateTime? Date { get; set; }

        public string? ServiceType { get; set; }

        public ReviewStatus? Status { get; set; }

        public string? Response { get; set; }
        public DateTime? ResponseDate { get; set; }
    }
}
