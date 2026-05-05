using System.ComponentModel.DataAnnotations;

namespace Core.DTO.Messaging
{
    // ====================================================================
    //  Resolver — used by n8n + internal services to know which sender to use
    // ====================================================================

    public class SmsSenderDecisionDTO
    {
        public bool CanSend { get; set; }
        public string? Reason { get; set; }                 // "TrialActive", "Approved", or block reason code
        public string? FromPhoneE164 { get; set; }
        public string? SenderSource { get; set; }           // "CompanyTwilioNumber" | "MaidsFlowTrialNumber"
        public int CompanyId { get; set; }
        public string Status { get; set; } = "Trial";       // mirror of CompanyMessagingProfile.Status
        public DateTime? TrialEndsAtUtc { get; set; }
        public string? TwilioMessagingServiceSid { get; set; }
    }

    // ====================================================================
    //  Profile (status overview)
    // ====================================================================

    public class CompanyMessagingProfileDTO
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public bool SmsEnabled { get; set; }
        public string Status { get; set; } = "Trial";
        public DateTime? TrialStartedAtUtc { get; set; }
        public DateTime? TrialEndsAtUtc { get; set; }
        public int? TrialDaysRemaining { get; set; }
        public string? DefaultTrialFromPhoneE164 { get; set; }
        public string? TwilioFromPhoneE164 { get; set; }
        public string? TwilioMessagingServiceSid { get; set; }
        public string? TwilioBrandSid { get; set; }
        public string? TwilioCampaignSid { get; set; }
        public DateTime? SubmittedToTwilioAtUtc { get; set; }
        public DateTime? ApprovedAtUtc { get; set; }
        public DateTime? RejectedAtUtc { get; set; }
        public string? RejectionReason { get; set; }
        public string? PublicConsentPageUrl { get; set; }
    }

    // ====================================================================
    //  Application — full payload (Brand + Campaign)
    // ====================================================================

    public class CompanyTwilioCampaignApplicationDTO
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }

        // Business
        public string LegalBusinessName { get; set; } = "";
        public string? DbaName { get; set; }
        public string? Ein { get; set; }
        public string BusinessType { get; set; } = "";
        public string BusinessWebsiteUrl { get; set; } = "";
        public string BusinessAddressLine1 { get; set; } = "";
        public string? BusinessAddressLine2 { get; set; }
        public string BusinessCity { get; set; } = "";
        public string BusinessState { get; set; } = "";
        public string BusinessPostalCode { get; set; } = "";
        public string BusinessCountry { get; set; } = "US";
        public string ContactFirstName { get; set; } = "";
        public string ContactLastName { get; set; } = "";
        public string ContactEmail { get; set; } = "";
        public string ContactPhoneE164 { get; set; } = "";

        // Campaign
        public string UseCase { get; set; } = "LOW_VOLUME";
        public string CampaignDescription { get; set; } = "";
        public string MessageFlow { get; set; } = "";
        public List<string> MessageSamples { get; set; } = new();
        public bool HasEmbeddedLinks { get; set; }
        public bool HasEmbeddedPhone { get; set; }
        public List<string> OptInKeywords { get; set; } = new() { "START" };
        public List<string> OptOutKeywords { get; set; } = new() { "STOP" };
        public List<string> HelpKeywords { get; set; } = new() { "HELP" };
        public string? OptInMessage { get; set; }
        public string OptOutMessage { get; set; } = "";
        public string HelpMessage { get; set; } = "";
        public string PublicConsentPageSlug { get; set; } = "";
        public string TermsUrl { get; set; } = "";
        public string PrivacyPolicyUrl { get; set; } = "";
        public string EstimatedMonthlyVolume { get; set; } = "1-1000";

        public string Status { get; set; } = "Draft";
        public string? AdminReviewNotes { get; set; }
        public DateTime? SubmittedAtUtc { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
    }

    public class CreateOrUpdateCampaignApplicationDTO
    {
        [MaxLength(255)] public string LegalBusinessName { get; set; } = "";
        [MaxLength(255)] public string? DbaName { get; set; }
        [MaxLength(64)]  public string? Ein { get; set; }
        [MaxLength(64)]  public string BusinessType { get; set; } = "";
        [MaxLength(500)] public string BusinessWebsiteUrl { get; set; } = "";
        [MaxLength(255)] public string BusinessAddressLine1 { get; set; } = "";
        [MaxLength(255)] public string? BusinessAddressLine2 { get; set; }
        [MaxLength(120)] public string BusinessCity { get; set; } = "";
        [MaxLength(120)] public string BusinessState { get; set; } = "";
        [MaxLength(20)]  public string BusinessPostalCode { get; set; } = "";
        [MaxLength(2)]   public string BusinessCountry { get; set; } = "US";
        [MaxLength(120)] public string ContactFirstName { get; set; } = "";
        [MaxLength(120)] public string ContactLastName { get; set; } = "";
        [MaxLength(255)] public string ContactEmail { get; set; } = "";
        [MaxLength(32)]  public string ContactPhoneE164 { get; set; } = "";

        [MaxLength(64)] public string UseCase { get; set; } = "LOW_VOLUME";
        [MaxLength(4096)] public string CampaignDescription { get; set; } = "";
        [MaxLength(2049)] public string MessageFlow { get; set; } = "";
        public List<string>? MessageSamples { get; set; }
        public bool HasEmbeddedLinks { get; set; }
        public bool HasEmbeddedPhone { get; set; }
        public List<string>? OptInKeywords { get; set; }
        public List<string>? OptOutKeywords { get; set; }
        public List<string>? HelpKeywords { get; set; }
        public string? OptInMessage { get; set; }
        public string? OptOutMessage { get; set; }
        public string? HelpMessage { get; set; }
        [MaxLength(120)] public string PublicConsentPageSlug { get; set; } = "";
        [MaxLength(500)] public string TermsUrl { get; set; } = "";
        [MaxLength(500)] public string PrivacyPolicyUrl { get; set; } = "";
        [MaxLength(64)]  public string EstimatedMonthlyVolume { get; set; } = "1-1000";
    }

    // ====================================================================
    //  Documents
    // ====================================================================

    public class CompanyTwilioDocumentDTO
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public int CampaignApplicationId { get; set; }
        public string DocumentType { get; set; } = "";
        public string FileUrl { get; set; } = "";
        public string OriginalFileName { get; set; } = "";
        public string ContentType { get; set; } = "";
        public string Status { get; set; } = "Pending";
        public string? RejectionReason { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class CreateDocumentDTO
    {
        [Required] public int CampaignApplicationId { get; set; }
        [Required, MaxLength(64)] public string DocumentType { get; set; } = "";
        [Required, MaxLength(1024)] public string FileUrl { get; set; } = "";
        [MaxLength(255)] public string OriginalFileName { get; set; } = "";
        [MaxLength(120)] public string ContentType { get; set; } = "";
    }

    // ====================================================================
    //  Public consent landing
    // ====================================================================

    public class PublicConsentLandingDTO
    {
        public string CompanyName { get; set; } = "";
        public string? CompanyLogoUrl { get; set; }
        public string Slug { get; set; } = "";
        public string TermsUrl { get; set; } = "";
        public string PrivacyPolicyUrl { get; set; } = "";
        public string ConsentText { get; set; } = "";
        public List<string> OptOutKeywords { get; set; } = new();
        public List<string> HelpKeywords { get; set; } = new();
        public bool IsActive { get; set; } = true;
    }

    public class AcceptConsentDTO
    {
        [Required, MaxLength(32)]
        public string PhoneE164 { get; set; } = "";

        [MaxLength(255)] public string? Name { get; set; }
        [MaxLength(255)] public string? Email { get; set; }
        public bool Agreed { get; set; }
    }

    // ====================================================================
    //  Admin review actions
    // ====================================================================

    public class AdminApplicationListItemDTO
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime? SubmittedAtUtc { get; set; }
        public DateTime? TrialEndsAtUtc { get; set; }
        public int DocumentsTotal { get; set; }
        public int DocumentsApproved { get; set; }
        public int DocumentsRejected { get; set; }
        public string? TwilioFromPhoneE164 { get; set; }
    }

    public class AdminSetStatusDTO
    {
        [Required, MaxLength(32)] public string Status { get; set; } = "";
        public string? Notes { get; set; }
    }

    public class AdminSetTwilioDTO
    {
        [MaxLength(32)] public string? TwilioFromPhoneE164 { get; set; }
        [MaxLength(64)] public string? TwilioPhoneNumberSid { get; set; }
        [MaxLength(64)] public string? TwilioMessagingServiceSid { get; set; }
        [MaxLength(64)] public string? TwilioBrandSid { get; set; }
        [MaxLength(64)] public string? TwilioCampaignSid { get; set; }
        [MaxLength(64)] public string? TwilioTrustProductSid { get; set; }
        [MaxLength(64)] public string? TwilioCustomerProfileSid { get; set; }
    }

    public class AdminReviewDocumentDTO
    {
        [Required, MaxLength(32)] public string Status { get; set; } = ""; // Approved | Rejected
        public string? RejectionReason { get; set; }
    }

    public class AdminAuditLogDTO
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public int? UserId { get; set; }
        public string Action { get; set; } = "";
        public string? Notes { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
    // ====================================================================
    //  Company-side: Business profile only (no campaign data)
    // ====================================================================

    public class CreateOrUpdateBusinessProfileDTO
    {
        [Required, MaxLength(255)] public string LegalBusinessName { get; set; } = "";
        [MaxLength(255)] public string? DbaName { get; set; }
        [MaxLength(64)]  public string? Ein { get; set; }
        [Required, MaxLength(64)]  public string BusinessType { get; set; } = "";
        [MaxLength(500)] public string BusinessWebsiteUrl { get; set; } = "";

        [Required, MaxLength(255)] public string BusinessAddressLine1 { get; set; } = "";
        [MaxLength(255)] public string? BusinessAddressLine2 { get; set; }
        [Required, MaxLength(120)] public string BusinessCity { get; set; } = "";
        [Required, MaxLength(120)] public string BusinessState { get; set; } = "";
        [Required, MaxLength(20)]  public string BusinessPostalCode { get; set; } = "";
        [MaxLength(2)]  public string BusinessCountry { get; set; } = "US";

        [Required, MaxLength(120)] public string ContactFirstName { get; set; } = "";
        [Required, MaxLength(120)] public string ContactLastName { get; set; } = "";
        [Required, EmailAddress, MaxLength(255)] public string ContactEmail { get; set; } = "";
        [Required, MaxLength(32)]  public string ContactPhoneE164 { get; set; } = "";

        // The company can OPTIONALLY provide their own Terms / Privacy URLs
        // (e.g. their own website). If not provided, the auto-generated landing
        // page hosts the consent flow without external links.
        [MaxLength(500)] public string? TermsUrl { get; set; }
        [MaxLength(500)] public string? PrivacyPolicyUrl { get; set; }

        [MaxLength(64)] public string? EstimatedMonthlyVolume { get; set; }
    }

    // ====================================================================
    //  Admin-side: Campaign data (only admin fills these in)
    // ====================================================================

    public class AdminUpdateCampaignDTO
    {
        [Required, MaxLength(64)]   public string UseCase { get; set; } = "LOW_VOLUME";
        [Required, MaxLength(4096)] public string CampaignDescription { get; set; } = "";
        [Required, MaxLength(2049)] public string MessageFlow { get; set; } = "";
        public List<string>? MessageSamples { get; set; }
        public bool HasEmbeddedLinks { get; set; }
        public bool HasEmbeddedPhone { get; set; }
        public List<string>? OptInKeywords { get; set; }
        public List<string>? OptOutKeywords { get; set; }
        public List<string>? HelpKeywords { get; set; }
        [MaxLength(2048)] public string? OptInMessage { get; set; }
        [MaxLength(2048)] public string? OptOutMessage { get; set; }
        [MaxLength(2048)] public string? HelpMessage { get; set; }
        [MaxLength(64)]   public string? EstimatedMonthlyVolume { get; set; }
    }

    // ====================================================================
    //  Documents — S3 presigned upload flow
    // ====================================================================

    public class PresignDocumentUploadDTO
    {
        [Required, MaxLength(64)]  public string DocumentType { get; set; } = "";
        [Required, MaxLength(255)] public string FileName { get; set; } = "";
        [Required, MaxLength(120)] public string ContentType { get; set; } = "";
    }

    public class PresignDocumentUploadResultDTO
    {
        public string Key { get; set; } = "";
        public string UploadUrl { get; set; } = "";
        public DateTimeOffset ExpiresAtUtc { get; set; }
        // Send this back to /documents/confirm (along with file metadata) once the PUT completes.
    }

    public class ConfirmDocumentUploadDTO
    {
        [Required, MaxLength(64)]   public string DocumentType { get; set; } = "";
        [Required, MaxLength(1024)] public string Key { get; set; } = "";
        [MaxLength(255)] public string OriginalFileName { get; set; } = "";
        [MaxLength(120)] public string ContentType { get; set; } = "";
    }

    // ====================================================================
    //  Landing page URL response
    // ====================================================================

    public class CompanyLandingUrlDTO
    {
        public bool IsAvailable { get; set; }      // false while application is Draft
        public string? Slug { get; set; }
        public string? RelativeUrl { get; set; }   // "/sms-consent?slug=..."
        public string? AbsoluteUrl { get; set; }   // full https URL based on PublicAppUrl config
    }
    // ====================================================================
    //  Admin: Message run logs (SMS + Email envelope log)
    // ====================================================================

    public class MessageRunListItemDTO
    {
        public int Id { get; set; }
        public int AppointmentId { get; set; }
        public int? CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public string Channel { get; set; } = "";       // "Email" | "Sms"
        public string Kind { get; set; } = "";          // "ConfirmationSms24h" | "ReminderEmail48h" | ...
        public string Status { get; set; } = "";       // "Pending" | "Sent" | "Failed" | "Skipped"
        public int Attempt { get; set; }
        public string? RecipientEmail { get; set; }
        public string? RecipientPhoneE164 { get; set; }
        public string? SenderPhoneE164 { get; set; }
        public string? SenderSource { get; set; }
        public string? Subject { get; set; }
        public string? Provider { get; set; }
        public string? ProviderStatus { get; set; }
        public string? LastError { get; set; }
        public bool WasBlockedByMessagingPolicy { get; set; }
        public string? MessagingBlockReason { get; set; }
        public DateTime? ScheduledForUtc { get; set; }
        public DateTime? SentAtUtc { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class MessageRunsPageDTO
    {
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<MessageRunListItemDTO> Items { get; set; } = new();
    }

    public class MessageRunStatsDTO
    {
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }
        public int Total { get; set; }
        public int Sent { get; set; }
        public int Failed { get; set; }
        public int Pending { get; set; }
        public int Skipped { get; set; }

        public int SmsTotal { get; set; }
        public int SmsSent { get; set; }
        public int SmsFailed { get; set; }

        public int EmailTotal { get; set; }
        public int EmailSent { get; set; }
        public int EmailFailed { get; set; }
    }
    // ====================================================================
    //  Admin Compliance Audit feed (CompanyMessagingAuditLogs union view)
    // ====================================================================

    public class ComplianceAuditItemDTO
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public int? UserId { get; set; }
        public string? UserName { get; set; }
        public string Action { get; set; } = "";
        public string? Notes { get; set; }
        public string? AfterJson { get; set; }
        public string? BeforeJson { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class ComplianceAuditPageDTO
    {
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<ComplianceAuditItemDTO> Items { get; set; } = new();
    }
    // ====================================================================
    //  Admin Documents Review (cross-company, grouped)
    // ====================================================================

    public class AdminDocumentItemDTO
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public int CampaignApplicationId { get; set; }
        public string DocumentType { get; set; } = "";
        public string FileUrl { get; set; } = "";        // signed download URL when returned
        public string OriginalFileName { get; set; } = "";
        public string ContentType { get; set; } = "";
        public string Status { get; set; } = "Pending";  // Pending | Approved | Rejected
        public string? RejectionReason { get; set; }
        public int? ReviewedByUserId { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class AdminDocumentsCompanyGroupDTO
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = "";
        public string ProfileStatus { get; set; } = "";   // mirror of CompanyMessagingProfile.Status
        public string ApplicationStatus { get; set; } = ""; // CompanyTwilioCampaignApplication.Status

        // Hosted public consent landing — populated when the application has a slug AND
        // is no longer in Draft. Absolute URL is built from App:PublicBaseUrl on the server.
        public string? LandingPageUrl { get; set; }

        public int Total { get; set; }
        public int Pending { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }

        // Business profile snapshot (read-only — company is the source of truth)
        public CompanyTwilioCampaignApplicationDTO? Application { get; set; }

        public List<AdminDocumentItemDTO> Documents { get; set; } = new();
    }

    public class AdminDocumentsPageDTO
    {
        public int TotalCompanies { get; set; }
        public int TotalDocuments { get; set; }
        public int PendingDocuments { get; set; }
        public List<AdminDocumentsCompanyGroupDTO> Groups { get; set; } = new();
    }
}
