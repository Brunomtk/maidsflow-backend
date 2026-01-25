// Core/Models/Review.cs
using System;
using Core.Enums;

namespace Core.Models
{
    public class Review : BaseModel
    {
        // IDs are persisted as integers across the system (Company/Customer/Professional/Team/Appointment).
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }

        public int? CustomerAddressId { get; set; }
        public CustomerAddress? CustomerAddress { get; set; }

        public int? ProfessionalId { get; set; }
        public string? ProfessionalName { get; set; }

        public int? TeamId { get; set; }
        public string? TeamName { get; set; }

        public int CompanyId { get; set; }
        public string? CompanyName { get; set; }

        public int AppointmentId { get; set; }

        /// <summary>
        /// Public identifier used to generate a shareable review link (no authentication required).
        /// It is generated server-side and must be treated as a secret.
        /// </summary>
        public Guid? PublicToken { get; set; }

        /// <summary>
        /// When the customer submitted the review through the public link.
        /// </summary>
        public DateTime? SubmittedAt { get; set; }

        public int Rating { get; set; }
        public string? Comment { get; set; }

        public DateTime Date { get; set; }

        public string ServiceType { get; set; } = string.Empty;

        public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

        public string? Response { get; set; }
        public DateTime? ResponseDate { get; set; }
    }
}
