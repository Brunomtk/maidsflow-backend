using System.ComponentModel.DataAnnotations;

namespace Core.Models
{
    /// <summary>
    /// Twilio A2P 10DLC Brand + Campaign application data submitted by a company.
    ///
    /// All fields here are designed to be the inputs for:
    ///   - Twilio Trust Hub Customer/Trust Profile
    ///   - Twilio Brand registration
    ///   - Twilio Campaign registration (us_app_to_person)
    ///
    /// Twilio's documented requirements:
    ///   - description: 40–4096 chars
    ///   - message_flow: 40–2049 chars
    ///   - message_samples: 2–5 samples, each 20–1024 chars
    /// </summary>
    public class CompanyTwilioCampaignApplication : BaseModel
    {
        public int CompanyId { get; set; }

        // --- Business Profile ---
        [MaxLength(255)]
        public string LegalBusinessName { get; set; } = "";

        [MaxLength(255)]
        public string? DbaName { get; set; }

        [MaxLength(64)]
        public string? Ein { get; set; }

        [MaxLength(64)]
        public string BusinessType { get; set; } = ""; // e.g. "LLC", "Sole Proprietor", "Corporation"

        [MaxLength(500)]
        public string BusinessWebsiteUrl { get; set; } = "";

        [MaxLength(255)]
        public string BusinessAddressLine1 { get; set; } = "";

        [MaxLength(255)]
        public string? BusinessAddressLine2 { get; set; }

        [MaxLength(120)]
        public string BusinessCity { get; set; } = "";

        [MaxLength(120)]
        public string BusinessState { get; set; } = "";

        [MaxLength(20)]
        public string BusinessPostalCode { get; set; } = "";

        [MaxLength(2)]
        public string BusinessCountry { get; set; } = "US";

        [MaxLength(120)]
        public string ContactFirstName { get; set; } = "";

        [MaxLength(120)]
        public string ContactLastName { get; set; } = "";

        [MaxLength(255)]
        public string ContactEmail { get; set; } = "";

        [MaxLength(32)]
        public string ContactPhoneE164 { get; set; } = "";

        // --- Campaign ---
        [MaxLength(64)]
        public string UseCase { get; set; } = "LOW_VOLUME"; // Twilio us_app_to_person_usecase enum

        /// <summary>Min 40, Max 4096 chars per Twilio spec.</summary>
        [MaxLength(4096)]
        public string CampaignDescription { get; set; } = "";

        /// <summary>Min 40, Max 2049 chars per Twilio spec — opt-in flow explanation.</summary>
        [MaxLength(2049)]
        public string MessageFlow { get; set; } = "";

        /// <summary>JSON array of 2–5 message templates (20–1024 chars each).</summary>
        public string MessageSamplesJson { get; set; } = "[]";

        public bool HasEmbeddedLinks { get; set; }
        public bool HasEmbeddedPhone { get; set; }

        // Keywords stored as JSON arrays so you can register multiple synonyms with Twilio
        [MaxLength(1024)]
        public string OptInKeywordsJson { get; set; } = "[\"START\"]";

        [MaxLength(1024)]
        public string OptOutKeywordsJson { get; set; } = "[\"STOP\"]";

        [MaxLength(1024)]
        public string HelpKeywordsJson { get; set; } = "[\"HELP\"]";

        [MaxLength(2048)]
        public string? OptInMessage { get; set; }

        [MaxLength(2048)]
        public string OptOutMessage { get; set; } =
            "You have successfully unsubscribed. You will no longer receive SMS messages.";

        [MaxLength(2048)]
        public string HelpMessage { get; set; } =
            "Reply STOP to unsubscribe. Contact the business directly for support.";

        // --- Public consent landing page ---
        [MaxLength(120)]
        public string PublicConsentPageSlug { get; set; } = "";

        [MaxLength(500)]
        public string TermsUrl { get; set; } = "";

        [MaxLength(500)]
        public string PrivacyPolicyUrl { get; set; } = "";

        [MaxLength(64)]
        public string EstimatedMonthlyVolume { get; set; } = "1-1000";

        // --- Workflow status ---
        /// <summary>Draft | PendingReview | NeedsChanges | ReadyForTwilio | SubmittedToTwilio | Approved | Rejected</summary>
        [MaxLength(32)]
        public string Status { get; set; } = "Draft";

        public string? AdminReviewNotes { get; set; }

        public DateTime? SubmittedAtUtc { get; set; }

        public Company Company { get; set; } = null!;
    }
}
