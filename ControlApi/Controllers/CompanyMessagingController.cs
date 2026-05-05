using System.Text.Json;
using Core.DTO.Messaging;
using Core.Models;
using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Services.Messaging;
using Services.Security;
using Services.Storage;

namespace ControlApi.Controllers;

/// <summary>
/// Company-facing endpoints for managing the company's own messaging compliance:
///   - View profile / status / trial countdown
///   - Submit Business profile data (Brand + contact)
///   - Upload supporting documents to S3 via presigned URLs
///   - Read the auto-generated public consent landing URL
///
/// IMPORTANT (new flow): the company does NOT fill in Campaign data anymore.
/// Campaign data (description, message flow, samples, opt-in/out keywords, etc.)
/// is filled in by the MaidsFlow admin during review. The company only sees
/// campaign + landing URL after the admin approves the application.
/// </summary>
[ApiController]
[Route("api/company/messaging")]
[Authorize]
public class CompanyMessagingController : ControllerBase
{
    private readonly DbContextClass _db;
    private readonly ICurrentUser _currentUser;
    private readonly IS3StorageService _s3;
    private readonly IConfiguration _config;

    public CompanyMessagingController(
        DbContextClass db,
        ICurrentUser currentUser,
        IS3StorageService s3,
        IConfiguration config)
    {
        _db = db;
        _currentUser = currentUser;
        _s3 = s3;
        _config = config;
    }

    private int? GetCompanyId() => _currentUser.CompanyId;

    private string PublicAppBaseUrl =>
        (_config["App:PublicBaseUrl"]
         ?? _config["PublicAppBaseUrl"]
         ?? "https://maidsflow.com").TrimEnd('/');

    // =================================================================
    //  Profile (status overview)
    // =================================================================

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var companyId = GetCompanyId();
        if (companyId == null) return Forbid();

        var profile = await _db.CompanyMessagingProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.CompanyId == companyId.Value, ct);

        var app = await _db.CompanyTwilioCampaignApplications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.CompanyId == companyId.Value, ct);

        var landingUrl = BuildLandingUrlOrNull(app);

        var dto = new CompanyMessagingProfileDTO
        {
            Id = profile?.Id ?? 0,
            CompanyId = companyId.Value,
            SmsEnabled = profile?.SmsEnabled ?? false,
            Status = profile?.Status ?? "Trial",
            TrialStartedAtUtc = profile?.TrialStartedAtUtc,
            TrialEndsAtUtc = profile?.TrialEndsAtUtc,
            TrialDaysRemaining = profile?.TrialEndsAtUtc.HasValue == true
                ? Math.Max(0, (int)(profile!.TrialEndsAtUtc!.Value - DateTime.UtcNow).TotalDays)
                : null,
            DefaultTrialFromPhoneE164 = profile?.DefaultTrialFromPhoneE164,
            TwilioFromPhoneE164 = profile?.TwilioFromPhoneE164,
            TwilioMessagingServiceSid = profile?.TwilioMessagingServiceSid,
            TwilioBrandSid = profile?.TwilioBrandSid,
            TwilioCampaignSid = profile?.TwilioCampaignSid,
            SubmittedToTwilioAtUtc = profile?.SubmittedToTwilioAtUtc,
            ApprovedAtUtc = profile?.ApprovedAtUtc,
            RejectedAtUtc = profile?.RejectedAtUtc,
            RejectionReason = profile?.RejectionReason,
            PublicConsentPageUrl = landingUrl,
        };

        return Ok(dto);
    }

    // =================================================================
    //  Application (read-only on the company side)
    //
    //  The company can view the current application — including any
    //  campaign data the admin filled in — but cannot edit campaign fields.
    // =================================================================

    [HttpGet("application")]
    public async Task<IActionResult> GetApplication(CancellationToken ct)
    {
        var companyId = GetCompanyId();
        if (companyId == null) return Forbid();

        var app = await _db.CompanyTwilioCampaignApplications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.CompanyId == companyId.Value, ct);

        if (app == null) return Ok((object?)null);
        return Ok(ToDto(app));
    }

    // =================================================================
    //  Business profile — the only editable surface for the company
    // =================================================================

    [HttpPost("business")]
    [HttpPut("business")]
    public async Task<IActionResult> UpsertBusinessProfile(
        [FromBody] CreateOrUpdateBusinessProfileDTO dto,
        CancellationToken ct)
    {
        var companyId = GetCompanyId();
        if (companyId == null) return Forbid();

        var app = await _db.CompanyTwilioCampaignApplications
            .FirstOrDefaultAsync(a => a.CompanyId == companyId.Value, ct);

        var isNew = app == null;
        if (app == null)
        {
            app = new CompanyTwilioCampaignApplication
            {
                CompanyId = companyId.Value,
                Status = "Draft",
                // Auto-generate the public consent slug ONCE on creation.
                // The company NEVER sees or controls this value.
                PublicConsentPageSlug = GenerateUniqueSlug(dto.LegalBusinessName, companyId.Value),
            };
            _db.CompanyTwilioCampaignApplications.Add(app);
        }
        else
        {
            // Block edits once the application has been submitted to admin/Twilio
            if (app.Status is "PendingReview" or "ReadyForTwilio" or "SubmittedToTwilio" or "Approved")
            {
                return Conflict(new
                {
                    error = "Application is locked because it is under review or already approved. " +
                            "Ask admin to set status back to NeedsChanges to edit."
                });
            }

            // Make sure a slug exists even on legacy rows
            if (string.IsNullOrWhiteSpace(app.PublicConsentPageSlug))
                app.PublicConsentPageSlug = GenerateUniqueSlug(dto.LegalBusinessName, companyId.Value);
        }

        // ---- Business / contact fields (the only ones the company controls) ----
        app.LegalBusinessName = dto.LegalBusinessName;
        app.DbaName = dto.DbaName;
        app.Ein = dto.Ein;
        app.BusinessType = dto.BusinessType;
        app.BusinessWebsiteUrl = dto.BusinessWebsiteUrl ?? "";
        app.BusinessAddressLine1 = dto.BusinessAddressLine1;
        app.BusinessAddressLine2 = dto.BusinessAddressLine2;
        app.BusinessCity = dto.BusinessCity;
        app.BusinessState = dto.BusinessState;
        app.BusinessPostalCode = dto.BusinessPostalCode;
        app.BusinessCountry = string.IsNullOrWhiteSpace(dto.BusinessCountry) ? "US" : dto.BusinessCountry;

        app.ContactFirstName = dto.ContactFirstName;
        app.ContactLastName = dto.ContactLastName;
        app.ContactEmail = dto.ContactEmail;
        app.ContactPhoneE164 = dto.ContactPhoneE164;

        // Optional: company can supply links to their OWN site's terms/privacy.
        // The hosted landing page is always auto-generated by MaidsFlow regardless.
        if (dto.TermsUrl != null) app.TermsUrl = dto.TermsUrl;
        if (dto.PrivacyPolicyUrl != null) app.PrivacyPolicyUrl = dto.PrivacyPolicyUrl;

        if (!string.IsNullOrWhiteSpace(dto.EstimatedMonthlyVolume))
            app.EstimatedMonthlyVolume = dto.EstimatedMonthlyVolume;

        await _db.SaveChangesAsync(ct);

        _db.CompanyMessagingAuditLogs.Add(new CompanyMessagingAuditLog
        {
            CompanyId = companyId.Value,
            UserId = _currentUser.UserId,
            Action = isNew ? "BusinessProfileCreated" : "BusinessProfileUpdated",
            Notes = $"Status remained {app.Status}",
            CreatedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(app));
    }

    // =================================================================
    //  Submit (move application to PendingReview for admin)
    //
    //  At submit time we only require Business + at least one document.
    //  Campaign fields are admin's responsibility post-submission.
    // =================================================================

    [HttpPost("application/submit")]
    public async Task<IActionResult> SubmitApplication(CancellationToken ct)
    {
        var companyId = GetCompanyId();
        if (companyId == null) return Forbid();

        var app = await _db.CompanyTwilioCampaignApplications
            .FirstOrDefaultAsync(a => a.CompanyId == companyId.Value, ct);
        if (app == null)
            return BadRequest(new { error = "Please complete the Business information first." });

        // Validate Business essentials only.
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(app.LegalBusinessName)) missing.Add("LegalBusinessName");
        if (string.IsNullOrWhiteSpace(app.BusinessType))      missing.Add("BusinessType");
        if (string.IsNullOrWhiteSpace(app.BusinessAddressLine1)) missing.Add("BusinessAddressLine1");
        if (string.IsNullOrWhiteSpace(app.BusinessCity))      missing.Add("BusinessCity");
        if (string.IsNullOrWhiteSpace(app.BusinessState))     missing.Add("BusinessState");
        if (string.IsNullOrWhiteSpace(app.BusinessPostalCode))missing.Add("BusinessPostalCode");
        if (string.IsNullOrWhiteSpace(app.ContactFirstName))  missing.Add("ContactFirstName");
        if (string.IsNullOrWhiteSpace(app.ContactLastName))   missing.Add("ContactLastName");
        if (string.IsNullOrWhiteSpace(app.ContactEmail))      missing.Add("ContactEmail");
        if (string.IsNullOrWhiteSpace(app.ContactPhoneE164))  missing.Add("ContactPhoneE164");

        if (missing.Count > 0)
            return BadRequest(new { error = "Missing required Business fields.", missing });

        var hasDocument = await _db.CompanyTwilioDocuments.AsNoTracking()
            .AnyAsync(d => d.CompanyId == companyId.Value, ct);
        if (!hasDocument)
            return BadRequest(new { error = "Please upload at least one document (EIN letter, license, etc.) before submitting." });

        if (string.IsNullOrWhiteSpace(app.PublicConsentPageSlug))
            app.PublicConsentPageSlug = GenerateUniqueSlug(app.LegalBusinessName, companyId.Value);

        app.Status = "PendingReview";
        app.SubmittedAtUtc = DateTime.UtcNow;

        // Sync messaging profile
        var profile = await _db.CompanyMessagingProfiles
            .FirstOrDefaultAsync(p => p.CompanyId == companyId.Value, ct);
        if (profile == null)
        {
            profile = new CompanyMessagingProfile
            {
                CompanyId = companyId.Value,
                SmsEnabled = true,
                Status = "PendingReview",
                TrialStartedAtUtc = DateTime.UtcNow,
                TrialEndsAtUtc = DateTime.UtcNow.AddDays(15),
            };
            _db.CompanyMessagingProfiles.Add(profile);
        }
        else
        {
            profile.Status = "PendingReview";
        }

        _db.CompanyMessagingAuditLogs.Add(new CompanyMessagingAuditLog
        {
            CompanyId = companyId.Value,
            UserId = _currentUser.UserId,
            Action = "ApplicationSubmitted",
            Notes = "Company submitted business profile + documents for admin review.",
            CreatedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true, status = "PendingReview" });
    }

    // =================================================================
    //  Documents
    // =================================================================

    [HttpGet("documents")]
    public async Task<IActionResult> GetDocuments(CancellationToken ct)
    {
        var companyId = GetCompanyId();
        if (companyId == null) return Forbid();

        var docs = await _db.CompanyTwilioDocuments.AsNoTracking()
            .Where(d => d.CompanyId == companyId.Value)
            .OrderByDescending(d => d.CreatedDate)
            .ToListAsync(ct);

        // Replace stored S3 keys with short-lived signed URLs for the frontend.
        var result = docs.Select(d =>
        {
            var dto = ToDto(d);
            // FileUrl in DB stores the S3 key. Translate to signed URL.
            var signed = _s3.CreateDownloadUrl(d.FileUrl);
            if (!string.IsNullOrWhiteSpace(signed)) dto.FileUrl = signed!;
            return dto;
        });

        return Ok(result);
    }

    /// <summary>
    /// Step 1 of upload: front-end POSTs document type + filename, gets back a
    /// presigned PUT URL. Front-end then uploads the bytes directly to S3.
    /// </summary>
    [HttpPost("documents/presign")]
    public IActionResult PresignDocumentUpload([FromBody] PresignDocumentUploadDTO dto)
    {
        var companyId = GetCompanyId();
        if (companyId == null) return Forbid();

        if (string.IsNullOrWhiteSpace(dto.FileName))
            return BadRequest(new { error = "FileName is required." });
        if (string.IsNullOrWhiteSpace(dto.DocumentType))
            return BadRequest(new { error = "DocumentType is required." });

        var presigned = _s3.CreateMessagingDocumentUploadUrl(
            companyId.Value, dto.DocumentType, dto.FileName, dto.ContentType ?? "application/octet-stream");

        return Ok(new PresignDocumentUploadResultDTO
        {
            Key = presigned.Key,
            UploadUrl = presigned.UploadUrl,
            ExpiresAtUtc = presigned.ExpiresAtUtc,
        });
    }

    /// <summary>
    /// Step 2 of upload: after the front-end successfully PUTs to S3, it confirms
    /// the upload here so we record the document row in the database.
    /// </summary>
    [HttpPost("documents/confirm")]
    public async Task<IActionResult> ConfirmDocumentUpload(
        [FromBody] ConfirmDocumentUploadDTO dto, CancellationToken ct)
    {
        var companyId = GetCompanyId();
        if (companyId == null) return Forbid();

        if (string.IsNullOrWhiteSpace(dto.Key))
            return BadRequest(new { error = "Key is required." });
        if (string.IsNullOrWhiteSpace(dto.DocumentType))
            return BadRequest(new { error = "DocumentType is required." });

        // Defense in depth: prevent companies from claiming keys outside their prefix
        var expectedPrefix = $"MessagingDocuments/{companyId.Value}/";
        if (!dto.Key.StartsWith("MessagingDocuments/", StringComparison.Ordinal) ||
            !dto.Key.Contains($"/{companyId.Value}/", StringComparison.Ordinal))
        {
            return BadRequest(new { error = "Invalid key for this company." });
        }

        // Make sure an application exists so we can link the document to it.
        var app = await _db.CompanyTwilioCampaignApplications
            .FirstOrDefaultAsync(a => a.CompanyId == companyId.Value, ct);
        if (app == null)
            return BadRequest(new { error = "Please complete the Business information first." });

        var doc = new CompanyTwilioDocument
        {
            CompanyId = companyId.Value,
            CampaignApplicationId = app.Id,
            DocumentType = dto.DocumentType,
            FileUrl = dto.Key, // we store the S3 KEY, not a URL — signed URLs are generated on read
            OriginalFileName = dto.OriginalFileName ?? "",
            ContentType = dto.ContentType ?? "",
            Status = "Pending",
        };
        _db.CompanyTwilioDocuments.Add(doc);

        _db.CompanyMessagingAuditLogs.Add(new CompanyMessagingAuditLog
        {
            CompanyId = companyId.Value,
            UserId = _currentUser.UserId,
            Action = "DocumentUploaded",
            Notes = $"{doc.DocumentType} — {doc.OriginalFileName}",
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);

        var resultDto = ToDto(doc);
        var signed = _s3.CreateDownloadUrl(doc.FileUrl);
        if (!string.IsNullOrWhiteSpace(signed)) resultDto.FileUrl = signed!;
        return Ok(resultDto);
    }

    [HttpDelete("documents/{id:int}")]
    public async Task<IActionResult> DeleteDocument(int id, CancellationToken ct)
    {
        var companyId = GetCompanyId();
        if (companyId == null) return Forbid();

        var doc = await _db.CompanyTwilioDocuments
            .FirstOrDefaultAsync(d => d.Id == id && d.CompanyId == companyId.Value, ct);
        if (doc == null) return NotFound();
        if (doc.Status == "Approved") return Conflict(new { error = "Cannot delete approved document." });

        // Best-effort delete from S3 (storage method is idempotent and swallows errors)
        try
        {
            if (_s3.TryGetKeyFromStoredValue(doc.FileUrl, out var key) && !string.IsNullOrWhiteSpace(key))
                await _s3.DeleteIfExistsAsync(key);
        }
        catch { /* swallow */ }

        _db.CompanyTwilioDocuments.Remove(doc);

        _db.CompanyMessagingAuditLogs.Add(new CompanyMessagingAuditLog
        {
            CompanyId = companyId.Value,
            UserId = _currentUser.UserId,
            Action = "DocumentDeleted",
            Notes = $"{doc.DocumentType} — {doc.OriginalFileName}",
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // =================================================================
    //  Landing page URL (auto-generated, never user-provided)
    // =================================================================

    [HttpGet("landing-url")]
    public async Task<IActionResult> GetLandingUrl(CancellationToken ct)
    {
        var companyId = GetCompanyId();
        if (companyId == null) return Forbid();

        var app = await _db.CompanyTwilioCampaignApplications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.CompanyId == companyId.Value, ct);

        // Available as soon as the company has submitted (PendingReview onward).
        // While the application is "Draft" we do not surface the link to keep the
        // UX clean (the company can't share a landing for an incomplete profile).
        var available = app != null
                        && !string.IsNullOrWhiteSpace(app.PublicConsentPageSlug)
                        && app.Status != "Draft";

        if (!available || app == null)
            return Ok(new CompanyLandingUrlDTO { IsAvailable = false });

        var slug = app.PublicConsentPageSlug;
        var rel = $"/sms-consent?slug={slug}";
        var abs = $"{PublicAppBaseUrl}{rel}";

        return Ok(new CompanyLandingUrlDTO
        {
            IsAvailable = true,
            Slug = slug,
            RelativeUrl = rel,
            AbsoluteUrl = abs,
        });
    }

    // =================================================================
    //  Helpers
    // =================================================================

    private string? BuildLandingUrlOrNull(CompanyTwilioCampaignApplication? app)
    {
        if (app == null || string.IsNullOrWhiteSpace(app.PublicConsentPageSlug)) return null;
        if (app.Status == "Draft") return null;
        return $"{PublicAppBaseUrl}/sms-consent?slug={app.PublicConsentPageSlug}";
    }

    /// <summary>
    /// Generates a deterministic, URL-safe, unique slug for a company's landing.
    /// Format: {kebab-business-name}-{companyId}.
    /// </summary>
    private string GenerateUniqueSlug(string businessName, int companyId)
    {
        var baseStr = (businessName ?? "").ToLowerInvariant();
        var clean = new string(baseStr
                .Select(c => char.IsLetterOrDigit(c) ? c : (c == ' ' || c == '-' || c == '_' ? '-' : '\0'))
                .Where(c => c != '\0')
                .ToArray())
            .Trim('-');
        while (clean.Contains("--")) clean = clean.Replace("--", "-");
        if (string.IsNullOrEmpty(clean)) clean = $"company-{companyId}";

        // Truncate the human-readable part to keep slug short
        if (clean.Length > 60) clean = clean.Substring(0, 60).TrimEnd('-');

        return $"{clean}-{companyId}";
    }

    private static CompanyTwilioCampaignApplicationDTO ToDto(CompanyTwilioCampaignApplication a) => new()
    {
        Id = a.Id,
        CompanyId = a.CompanyId,
        LegalBusinessName = a.LegalBusinessName,
        DbaName = a.DbaName,
        Ein = a.Ein,
        BusinessType = a.BusinessType,
        BusinessWebsiteUrl = a.BusinessWebsiteUrl,
        BusinessAddressLine1 = a.BusinessAddressLine1,
        BusinessAddressLine2 = a.BusinessAddressLine2,
        BusinessCity = a.BusinessCity,
        BusinessState = a.BusinessState,
        BusinessPostalCode = a.BusinessPostalCode,
        BusinessCountry = a.BusinessCountry,
        ContactFirstName = a.ContactFirstName,
        ContactLastName = a.ContactLastName,
        ContactEmail = a.ContactEmail,
        ContactPhoneE164 = a.ContactPhoneE164,
        UseCase = a.UseCase,
        CampaignDescription = a.CampaignDescription,
        MessageFlow = a.MessageFlow,
        MessageSamples = SafeDeserializeList(a.MessageSamplesJson),
        HasEmbeddedLinks = a.HasEmbeddedLinks,
        HasEmbeddedPhone = a.HasEmbeddedPhone,
        OptInKeywords = SafeDeserializeList(a.OptInKeywordsJson),
        OptOutKeywords = SafeDeserializeList(a.OptOutKeywordsJson),
        HelpKeywords = SafeDeserializeList(a.HelpKeywordsJson),
        OptInMessage = a.OptInMessage,
        OptOutMessage = a.OptOutMessage,
        HelpMessage = a.HelpMessage,
        PublicConsentPageSlug = a.PublicConsentPageSlug,
        TermsUrl = a.TermsUrl,
        PrivacyPolicyUrl = a.PrivacyPolicyUrl,
        EstimatedMonthlyVolume = a.EstimatedMonthlyVolume,
        Status = a.Status,
        AdminReviewNotes = a.AdminReviewNotes,
        SubmittedAtUtc = a.SubmittedAtUtc,
        CreatedDate = a.CreatedDate,
        UpdatedDate = a.UpdatedDate,
    };

    private static CompanyTwilioDocumentDTO ToDto(CompanyTwilioDocument d) => new()
    {
        Id = d.Id,
        CompanyId = d.CompanyId,
        CampaignApplicationId = d.CampaignApplicationId,
        DocumentType = d.DocumentType,
        FileUrl = d.FileUrl, // overwritten with signed URL by callers when needed
        OriginalFileName = d.OriginalFileName,
        ContentType = d.ContentType,
        Status = d.Status,
        RejectionReason = d.RejectionReason,
        ReviewedAtUtc = d.ReviewedAtUtc,
        CreatedDate = d.CreatedDate,
    };

    private static List<string> SafeDeserializeList(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); } catch { return new(); }
    }
}
