using System.ComponentModel.DataAnnotations;

namespace Core.Models
{
    /// <summary>
    /// Per-company SMS / messaging compliance profile.
    /// Tracks: trial state, Twilio number/SIDs (after approval), and current status.
    ///
    /// Status flow:
    ///   Trial → PendingReview → NeedsChanges (loop) → ReadyForTwilio →
    ///   SubmittedToTwilio → Approved | Rejected | Suspended | ExpiredTrial
    /// </summary>
    public class CompanyMessagingProfile : BaseModel
    {
        public int CompanyId { get; set; }

        /// <summary>Master toggle. False = company has not opted into SMS at all.</summary>
        public bool SmsEnabled { get; set; } = false;

        /// <summary>
        /// Trial | PendingReview | NeedsChanges | ReadyForTwilio
        /// SubmittedToTwilio | Approved | Rejected | ExpiredTrial | Suspended
        /// </summary>
        [MaxLength(32)]
        public string Status { get; set; } = "Trial";

        // --- Trial window (15 days from start) ---
        public DateTime? TrialStartedAtUtc { get; set; }
        public DateTime? TrialEndsAtUtc { get; set; }

        /// <summary>Phone used during trial — usually the MaidsFlow shared sandbox number.</summary>
        [MaxLength(32)]
        public string? DefaultTrialFromPhoneE164 { get; set; }

        // --- Approved company-owned Twilio assets ---
        [MaxLength(32)]
        public string? TwilioFromPhoneE164 { get; set; }

        [MaxLength(64)]
        public string? TwilioPhoneNumberSid { get; set; }

        [MaxLength(64)]
        public string? TwilioMessagingServiceSid { get; set; }

        [MaxLength(64)]
        public string? TwilioBrandSid { get; set; }

        [MaxLength(64)]
        public string? TwilioCampaignSid { get; set; }

        [MaxLength(64)]
        public string? TwilioTrustProductSid { get; set; }

        [MaxLength(64)]
        public string? TwilioCustomerProfileSid { get; set; }

        // --- Lifecycle timestamps ---
        public DateTime? SubmittedToTwilioAtUtc { get; set; }
        public DateTime? ApprovedAtUtc { get; set; }
        public DateTime? RejectedAtUtc { get; set; }

        [MaxLength(2048)]
        public string? RejectionReason { get; set; }

        public string? InternalAdminNotes { get; set; }

        public Company Company { get; set; } = null!;
    }
}
