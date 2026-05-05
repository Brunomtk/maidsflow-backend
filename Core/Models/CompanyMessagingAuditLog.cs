using System.ComponentModel.DataAnnotations;

namespace Core.Models
{
    /// <summary>
    /// Immutable audit trail of every state-change made to a company's messaging compliance
    /// (status transitions, document approvals, twilio number assignments, etc.).
    /// Used by the admin "Messaging Compliance" view.
    /// </summary>
    public class CompanyMessagingAuditLog : BaseModel
    {
        public int CompanyId { get; set; }

        public int? UserId { get; set; }

        /// <summary>
        /// Verb-like action key, e.g.: "ApplicationSubmitted", "StatusChanged",
        /// "DocumentApproved", "DocumentRejected", "TwilioNumberSet",
        /// "MessagingApproved", "MessagingRejected", "MessagingSuspended",
        /// "MessagingReactivated", "ConsentRecorded", "TrialStarted", "TrialExpired"
        /// </summary>
        [MaxLength(64)]
        public string Action { get; set; } = "";

        /// <summary>JSON snapshot of relevant state BEFORE the change.</summary>
        public string? BeforeJson { get; set; }

        /// <summary>JSON snapshot of relevant state AFTER the change.</summary>
        public string? AfterJson { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public Company Company { get; set; } = null!;
    }
}
