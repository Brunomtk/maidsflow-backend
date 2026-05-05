using System.ComponentModel.DataAnnotations;

namespace Core.Models
{
    /// <summary>
    /// Audit-grade snapshot of a customer's SMS consent given on a company's public landing
    /// page (/sms-consent/{slug}). Required as proof for Twilio compliance reviews.
    /// </summary>
    public class CompanySmsConsentRecord : BaseModel
    {
        public int CompanyId { get; set; }

        [MaxLength(120)]
        public string LandingSlug { get; set; } = "";

        [MaxLength(255)]
        public string? Name { get; set; }

        [MaxLength(255)]
        public string? Email { get; set; }

        [MaxLength(32)]
        public string PhoneE164 { get; set; } = "";

        /// <summary>Snapshot of the actual consent text shown on the landing at the time of acceptance.</summary>
        public string ConsentTextSnapshot { get; set; } = "";

        [MaxLength(500)]
        public string TermsUrl { get; set; } = "";

        [MaxLength(500)]
        public string PrivacyPolicyUrl { get; set; } = "";

        [MaxLength(32)]
        public string TermsVersion { get; set; } = "v1";

        [MaxLength(32)]
        public string PrivacyVersion { get; set; } = "v1";

        [MaxLength(64)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        public DateTime AcceptedAtUtc { get; set; }

        public Company Company { get; set; } = null!;
    }
}
