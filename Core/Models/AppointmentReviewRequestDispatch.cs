using System;
using Core.Enums;

namespace Core.Models
{
    /// <summary>
    /// Idempotency + retry log for automatic review-request emails.
    /// One dispatch is created per AppointmentCompletion (i.e., per completed occurrence).
    /// </summary>
    public class AppointmentReviewRequestDispatch : BaseModel
    {
        public int CompanyId { get; set; }

        public int AppointmentCompletionId { get; set; }
        public AppointmentCompletion? AppointmentCompletion { get; set; }

        public int ReviewId { get; set; }
        public Review? Review { get; set; }

        public int CustomerId { get; set; }
        public string RecipientEmail { get; set; } = string.Empty;

        public ReviewRequestDispatchStatus Status { get; set; } = ReviewRequestDispatchStatus.Pending;

        public int AttemptCount { get; set; } = 0;
        public DateTime? LastAttemptAtUtc { get; set; }
        public DateTime? SentAtUtc { get; set; }
        public string? LastError { get; set; }
    }
}
