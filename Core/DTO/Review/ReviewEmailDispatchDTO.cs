using System;

namespace Core.DTO.Review
{
    public class ReviewEmailDispatchDTO
    {
        public int ReviewId { get; set; }
        public Guid Token { get; set; }
        public string Url { get; set; } = string.Empty;
        public int AppointmentId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string RecipientEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public DateTime SentAtUtc { get; set; }
    }
}
