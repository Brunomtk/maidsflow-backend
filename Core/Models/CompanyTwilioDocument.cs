using System.ComponentModel.DataAnnotations;

namespace Core.Models
{
    /// <summary>
    /// Documents uploaded by a company in support of their A2P 10DLC application
    /// (EIN letter, business license, opt-in screenshots, etc.).
    /// Files are stored in S3; FileUrl holds the canonical S3 key or presignable URL.
    /// </summary>
    public class CompanyTwilioDocument : BaseModel
    {
        public int CompanyId { get; set; }

        public int CampaignApplicationId { get; set; }

        /// <summary>
        /// BusinessProof | EinLetter | BusinessLicense | OptInScreenshot
        /// ConsentPageScreenshot | PrivacyPolicyScreenshot | TermsScreenshot | Other
        /// </summary>
        [MaxLength(64)]
        public string DocumentType { get; set; } = "";

        [MaxLength(1024)]
        public string FileUrl { get; set; } = "";

        [MaxLength(255)]
        public string OriginalFileName { get; set; } = "";

        [MaxLength(120)]
        public string ContentType { get; set; } = "";

        /// <summary>Pending | Approved | Rejected</summary>
        [MaxLength(32)]
        public string Status { get; set; } = "Pending";

        [MaxLength(2048)]
        public string? RejectionReason { get; set; }

        public int? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }

        public Company Company { get; set; } = null!;
    }
}
