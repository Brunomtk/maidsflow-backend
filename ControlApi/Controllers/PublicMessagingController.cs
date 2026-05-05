using System.Text.Json;
using Core.DTO.Messaging;
using Core.Models;
using Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.Messaging;

namespace ControlApi.Controllers;

/// <summary>
/// PUBLIC endpoints (no auth) — used by:
///  - The /sms-consent/{slug} landing page (GET + POST accept)
///  - The n8n SMS workflow to resolve the right sender + know if it can send
/// </summary>
[ApiController]
[Route("api")]
public class PublicMessagingController : ControllerBase
{
    private readonly DbContextClass _db;
    private readonly ISmsSenderResolver _resolver;

    public PublicMessagingController(DbContextClass db, ISmsSenderResolver resolver)
    {
        _db = db;
        _resolver = resolver;
    }

    // =====================================================================
    //  PUBLIC CONSENT LANDING
    // =====================================================================

    [HttpGet("public/messaging/{slug}")]
    public async Task<IActionResult> GetLanding(string slug, CancellationToken ct)
    {
        var app = await _db.CompanyTwilioCampaignApplications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.PublicConsentPageSlug == slug, ct);
        if (app == null) return NotFound();

        // Block the landing only when admin has explicitly Rejected or Suspended the company.
        // Draft / PendingReview / NeedsChanges / ReadyForTwilio / Approved all keep it public —
        // the URL itself is harmless and empresas usam pra preview e testes.
        if (app.Status == "Rejected" || app.Status == "Suspended") return NotFound();

        var company = await _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == app.CompanyId, ct);
        if (company == null) return NotFound();

        var optOut = SafeList(app.OptOutKeywordsJson);
        var help = SafeList(app.HelpKeywordsJson);

        var consentText =
            $"By entering your phone number and checking the box below, you agree to receive SMS messages from {company.Name} about appointment reminders, schedule updates, arrival updates, and service-related notifications. " +
            "Message frequency may vary based on your appointments. Message and data rates may apply. Reply STOP to unsubscribe. Reply HELP for help.";

        var dto = new PublicConsentLandingDTO
        {
            CompanyName = company.Name,
            CompanyLogoUrl = null,
            Slug = slug,
            TermsUrl = app.TermsUrl,
            PrivacyPolicyUrl = app.PrivacyPolicyUrl,
            ConsentText = consentText,
            OptOutKeywords = optOut,
            HelpKeywords = help,
            IsActive = app.Status is "Approved" or "PendingReview" or "ReadyForTwilio" or "SubmittedToTwilio",
        };

        return Ok(dto);
    }

    [HttpPost("public/messaging/{slug}/accept")]
    public async Task<IActionResult> AcceptConsent(string slug, [FromBody] AcceptConsentDTO dto, CancellationToken ct)
    {
        if (!dto.Agreed) return BadRequest(new { error = "You must accept the consent to proceed." });
        if (string.IsNullOrWhiteSpace(dto.PhoneE164))
            return BadRequest(new { error = "Phone is required." });

        var app = await _db.CompanyTwilioCampaignApplications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.PublicConsentPageSlug == slug, ct);
        if (app == null) return NotFound();
        if (app.Status == "Rejected" || app.Status == "Suspended") return NotFound();

        var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == app.CompanyId, ct);
        if (company == null) return NotFound();

        var consentText =
            $"By entering your phone number and checking the box below, you agree to receive SMS messages from {company.Name} about appointment reminders, schedule updates, arrival updates, and service-related notifications. " +
            "Message frequency may vary based on your appointments. Message and data rates may apply. Reply STOP to unsubscribe. Reply HELP for help.";

        var record = new CompanySmsConsentRecord
        {
            CompanyId = app.CompanyId,
            LandingSlug = slug,
            Name = dto.Name,
            Email = dto.Email,
            PhoneE164 = NormalizePhone(dto.PhoneE164),
            ConsentTextSnapshot = consentText,
            TermsUrl = app.TermsUrl,
            PrivacyPolicyUrl = app.PrivacyPolicyUrl,
            TermsVersion = "v1",
            PrivacyVersion = "v1",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            AcceptedAtUtc = DateTime.UtcNow,
        };
        _db.CompanySmsConsentRecords.Add(record);

        _db.CompanyMessagingAuditLogs.Add(new CompanyMessagingAuditLog
        {
            CompanyId = app.CompanyId,
            Action = "ConsentRecorded",
            Notes = $"Phone {record.PhoneE164} accepted via /sms-consent/{slug}",
            AfterJson = JsonSerializer.Serialize(new { record.PhoneE164, record.Name, record.AcceptedAtUtc }),
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true, recordedAtUtc = record.AcceptedAtUtc });
    }

    // =====================================================================
    //  n8n SENDER RESOLVER
    // =====================================================================

    /// <summary>
    /// Used by n8n (and any internal SMS sender) to ask: "Can company X send right now,
    /// and which Twilio number should be used?"
    /// </summary>
    [HttpGet("companies/{companyId:int}/messaging/sms-sender")]
    public async Task<IActionResult> GetSmsSender(int companyId, CancellationToken ct)
    {
        var decision = await _resolver.ResolveAsync(companyId, ct);
        return Ok(decision);
    }

    private static List<string> SafeList(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); } catch { return new(); }
    }

    private static string NormalizePhone(string raw)
    {
        var digits = new string((raw ?? "").Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(digits)) return "";
        if (digits.Length == 10) return "+1" + digits;
        if (digits.Length == 11 && digits.StartsWith("1")) return "+" + digits;
        return raw.StartsWith("+") ? raw : "+" + digits;
    }
}
