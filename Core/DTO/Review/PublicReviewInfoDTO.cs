using System;
using Core.Enums;

namespace Core.DTO.Review
{
    /// <summary>
    /// Data returned to the public review form (accessed by a token link).
    /// </summary>
    public class PublicReviewInfoDTO
    {
        public Guid Token { get; set; }
        public int ReviewId { get; set; }
        public int AppointmentId { get; set; }

        public int CompanyId { get; set; }
        public string? CompanyName { get; set; }

        public string? CustomerName { get; set; }
        public string? ProfessionalName { get; set; }

        public DateTime AppointmentStart { get; set; }

        public ReviewStatus Status { get; set; }
        public bool CanSubmit { get; set; }

        public int? Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime? SubmittedAt { get; set; }
    }
}
