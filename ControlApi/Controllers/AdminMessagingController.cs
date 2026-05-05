using System.Text.Json;
using Core.DTO.Messaging;
using Core.Enums.Messaging;
using Core.Models;
using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.Messaging;
using Services.Storage;
using Microsoft.Extensions.Configuration;
using Services.Security;

namespace ControlApi.Controllers;

/// <summary>
/// Admin endpoints to review/approve/reject company messaging compliance applications.
/// All actions are audited via CompanyMessagingAuditLog.
/// </summary>
[ApiController]
[Route("api/admin/messaging")]
[Authorize]
public class AdminMessagingController : ControllerBase
{
    private readonly DbContextClass _db;
    private readonly ICurrentUser _currentUser;

    private readonly IMessagingTodayService _today;
    private readonly IS3StorageService _s3;
    private readonly IConfiguration _config;

    public AdminMessagingController(DbContextClass db, ICurrentUser currentUser, IMessagingTodayService today, IS3StorageService s3, IConfiguration config)
    {
        _db = db;
        _currentUser = currentUser;
        _today = today;
        _s3 = s3;
        _config = config;
    }

    private string PublicAppBaseUrl =>
        (_config["App:PublicBaseUrl"]
         ?? _config["PublicAppBaseUrl"]
         ?? "https://maidsflow.com").TrimEnd('/');

    // ----- List applications by status -----

    [HttpGet("applications")]
    public async Task<IActionResult> ListApplications([FromQuery] string? status, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin) return Forbid();
        var q = _db.CompanyTwilioCampaignApplications
            .AsNoTracking()
            .Join(_db.Companies, a => a.CompanyId, c => c.Id, (a, c) => new { App = a, Company = c })
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
            q = q.Where(x => x.App.Status == status);

        var rows = await q.OrderByDescending(x => x.App.SubmittedAtUtc ?? x.App.UpdatedDate).ToListAsync(ct);

        var ids = rows.Select(r => r.App.Id).ToList();
        var docs = await _db.CompanyTwilioDocuments.AsNoTracking()
            .Where(d => ids.Contains(d.CampaignApplicationId))
            .GroupBy(d => new { d.CampaignApplicationId, d.Status })
            .Select(g => new { g.Key.CampaignApplicationId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        var profiles = await _db.CompanyMessagingProfiles.AsNoTracking()
            .Where(p => rows.Select(r => r.App.CompanyId).Contains(p.CompanyId))
            .ToListAsync(ct);

        var result = rows.Select(r =>
        {
            var prof = profiles.FirstOrDefault(p => p.CompanyId == r.App.CompanyId);
            var docCounts = docs.Where(d => d.CampaignApplicationId == r.App.Id).ToList();
            return new AdminApplicationListItemDTO
            {
                CompanyId = r.Company.Id,
                CompanyName = r.Company.Name,
                Status = r.App.Status,
                SubmittedAtUtc = r.App.SubmittedAtUtc,
                TrialEndsAtUtc = prof?.TrialEndsAtUtc,
                DocumentsTotal = docCounts.Sum(d => d.Count),
                DocumentsApproved = docCounts.FirstOrDefault(d => d.Status == "Approved")?.Count ?? 0,
                DocumentsRejected = docCounts.FirstOrDefault(d => d.Status == "Rejected")?.Count ?? 0,
                TwilioFromPhoneE164 = prof?.TwilioFromPhoneE164,
            };
        });

        return Ok(result);
    }

    // ----- Single application detail -----

    [HttpGet("applications/{companyId:int}")]
    public async Task<IActionResult> GetApplicationByCompany(int companyId, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin) return Forbid();
        var app = await _db.CompanyTwilioCampaignApplications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.CompanyId == companyId, ct);
        var profile = await _db.CompanyMessagingProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.CompanyId == companyId, ct);
        var docs = await _db.CompanyTwilioDocuments.AsNoTracking()
            .Where(d => d.CompanyId == companyId)
            .OrderByDescending(d => d.CreatedDate).ToListAsync(ct);

        return Ok(new
        {
            application = app,
            profile = profile,
            documents = docs,
        });
    }

    // ----- Set application status (PendingReview/NeedsChanges/ReadyForTwilio/SubmittedToTwilio) -----

    [HttpPatch("applications/{companyId:int}/status")]
    public async Task<IActionResult> SetApplicationStatus(int companyId, [FromBody] AdminSetStatusDTO dto, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin) return Forbid();
        var app = await _db.CompanyTwilioCampaignApplications
            .FirstOrDefaultAsync(a => a.CompanyId == companyId, ct);
        if (app == null) return NotFound();

        var before = app.Status;
        app.Status = dto.Status;
        app.AdminReviewNotes = dto.Notes;

        // Mirror to messaging profile when applicable
        var profile = await _db.CompanyMessagingProfiles
            .FirstOrDefaultAsync(p => p.CompanyId == companyId, ct);
        if (profile != null)
        {
            if (dto.Status is "PendingReview" or "NeedsChanges" or "ReadyForTwilio" or "SubmittedToTwilio")
                profile.Status = dto.Status;
            if (dto.Status == "SubmittedToTwilio") profile.SubmittedToTwilioAtUtc = DateTime.UtcNow;
        }

        _db.CompanyMessagingAuditLogs.Add(new CompanyMessagingAuditLog
        {
            CompanyId = companyId,
            UserId = _currentUser.UserId,
            Action = "StatusChanged",
            BeforeJson = JsonSerializer.Serialize(new { Status = before }),
            AfterJson = JsonSerializer.Serialize(new { Status = app.Status }),
            Notes = dto.Notes,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true, status = app.Status });
    }

    // ----- Approve / Reject / Suspend / Reactivate messaging at the company level -----

    [HttpPost("companies/{companyId:int}/approve")]
    public async Task<IActionResult> ApproveMessaging(int companyId, [FromBody] AdminSetTwilioDTO twilio, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin) return Forbid();
        var profile = await _db.CompanyMessagingProfiles
            .FirstOrDefaultAsync(p => p.CompanyId == companyId, ct);
        if (profile == null)
        {
            profile = new CompanyMessagingProfile { CompanyId = companyId };
            _db.CompanyMessagingProfiles.Add(profile);
        }

        var before = profile.Status;
        profile.Status = "Approved";
        profile.SmsEnabled = true;
        profile.ApprovedAtUtc = DateTime.UtcNow;
        profile.RejectedAtUtc = null;
        profile.RejectionReason = null;
        profile.TwilioFromPhoneE164 = twilio.TwilioFromPhoneE164 ?? profile.TwilioFromPhoneE164;
        profile.TwilioPhoneNumberSid = twilio.TwilioPhoneNumberSid ?? profile.TwilioPhoneNumberSid;
        profile.TwilioMessagingServiceSid = twilio.TwilioMessagingServiceSid ?? profile.TwilioMessagingServiceSid;
        profile.TwilioBrandSid = twilio.TwilioBrandSid ?? profile.TwilioBrandSid;
        profile.TwilioCampaignSid = twilio.TwilioCampaignSid ?? profile.TwilioCampaignSid;
        profile.TwilioTrustProductSid = twilio.TwilioTrustProductSid ?? profile.TwilioTrustProductSid;
        profile.TwilioCustomerProfileSid = twilio.TwilioCustomerProfileSid ?? profile.TwilioCustomerProfileSid;

        var app = await _db.CompanyTwilioCampaignApplications.FirstOrDefaultAsync(a => a.CompanyId == companyId, ct);
        if (app != null) app.Status = "Approved";

        _db.CompanyMessagingAuditLogs.Add(new CompanyMessagingAuditLog
        {
            CompanyId = companyId,
            UserId = _currentUser.UserId,
            Action = "MessagingApproved",
            BeforeJson = JsonSerializer.Serialize(new { Status = before }),
            AfterJson = JsonSerializer.Serialize(new { Status = profile.Status, profile.TwilioFromPhoneE164 }),
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    [HttpPost("companies/{companyId:int}/reject")]
    public async Task<IActionResult> RejectMessaging(int companyId, [FromBody] AdminSetStatusDTO dto, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin) return Forbid();
        var profile = await _db.CompanyMessagingProfiles.FirstOrDefaultAsync(p => p.CompanyId == companyId, ct);
        if (profile == null) return NotFound();

        var before = profile.Status;
        profile.Status = "Rejected";
        profile.RejectedAtUtc = DateTime.UtcNow;
        profile.RejectionReason = dto.Notes;

        _db.CompanyMessagingAuditLogs.Add(new CompanyMessagingAuditLog
        {
            CompanyId = companyId,
            UserId = _currentUser.UserId,
            Action = "MessagingRejected",
            BeforeJson = JsonSerializer.Serialize(new { Status = before }),
            AfterJson = JsonSerializer.Serialize(new { Status = profile.Status }),
            Notes = dto.Notes,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    [HttpPost("companies/{companyId:int}/suspend")]
    public async Task<IActionResult> SuspendMessaging(int companyId, [FromBody] AdminSetStatusDTO dto, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin) return Forbid();
        var profile = await _db.CompanyMessagingProfiles.FirstOrDefaultAsync(p => p.CompanyId == companyId, ct);
        if (profile == null) return NotFound();

        var before = profile.Status;
        profile.Status = "Suspended";

        _db.CompanyMessagingAuditLogs.Add(new CompanyMessagingAuditLog
        {
            CompanyId = companyId,
            UserId = _currentUser.UserId,
            Action = "MessagingSuspended",
            BeforeJson = JsonSerializer.Serialize(new { Status = before }),
            AfterJson = JsonSerializer.Serialize(new { Status = profile.Status }),
            Notes = dto.Notes,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    [HttpPost("companies/{companyId:int}/reactivate")]
    public async Task<IActionResult> ReactivateMessaging(int companyId, [FromBody] AdminSetStatusDTO dto, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin) return Forbid();
        var profile = await _db.CompanyMessagingProfiles.FirstOrDefaultAsync(p => p.CompanyId == companyId, ct);
        if (profile == null) return NotFound();

        var before = profile.Status;
        profile.Status = !string.IsNullOrWhiteSpace(profile.TwilioFromPhoneE164) ? "Approved" : "PendingReview";

        _db.CompanyMessagingAuditLogs.Add(new CompanyMessagingAuditLog
        {
            CompanyId = companyId,
            UserId = _currentUser.UserId,
            Action = "MessagingReactivated",
            BeforeJson = JsonSerializer.Serialize(new { Status = before }),
            AfterJson = JsonSerializer.Serialize(new { Status = profile.Status }),
            Notes = dto.Notes,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    [HttpPut("companies/{companyId:int}/twilio")]
    public async Task<IActionResult> SetTwilio(int companyId, [FromBody] AdminSetTwilioDTO twilio, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin) return Forbid();
        var profile = await _db.CompanyMessagingProfiles.FirstOrDefaultAsync(p => p.CompanyId == companyId, ct);
        if (profile == null)
        {
            profile = new CompanyMessagingProfile { CompanyId = companyId, Status = "PendingReview" };
            _db.CompanyMessagingProfiles.Add(profile);
        }

        var before = JsonSerializer.Serialize(new { profile.TwilioFromPhoneE164, profile.TwilioMessagingServiceSid, profile.TwilioBrandSid, profile.TwilioCampaignSid });
        profile.TwilioFromPhoneE164 = twilio.TwilioFromPhoneE164 ?? profile.TwilioFromPhoneE164;
        profile.TwilioPhoneNumberSid = twilio.TwilioPhoneNumberSid ?? profile.TwilioPhoneNumberSid;
        profile.TwilioMessagingServiceSid = twilio.TwilioMessagingServiceSid ?? profile.TwilioMessagingServiceSid;
        profile.TwilioBrandSid = twilio.TwilioBrandSid ?? profile.TwilioBrandSid;
        profile.TwilioCampaignSid = twilio.TwilioCampaignSid ?? profile.TwilioCampaignSid;
        profile.TwilioTrustProductSid = twilio.TwilioTrustProductSid ?? profile.TwilioTrustProductSid;
        profile.TwilioCustomerProfileSid = twilio.TwilioCustomerProfileSid ?? profile.TwilioCustomerProfileSid;

        _db.CompanyMessagingAuditLogs.Add(new CompanyMessagingAuditLog
        {
            CompanyId = companyId,
            UserId = _currentUser.UserId,
            Action = "TwilioNumberSet",
            BeforeJson = before,
            AfterJson = JsonSerializer.Serialize(twilio),
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    // ----- Campaign data (admin-only — company never edits these fields) -----

    [HttpGet("applications/{companyId:int}/campaign")]
    public async Task<IActionResult> GetCampaign(int companyId, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin) return Forbid();
        var app = await _db.CompanyTwilioCampaignApplications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.CompanyId == companyId, ct);
        if (app == null) return NotFound();
        return Ok(new
        {
            app.UseCase,
            app.CampaignDescription,
            app.MessageFlow,
            MessageSamples = SafeList(app.MessageSamplesJson),
            app.HasEmbeddedLinks,
            app.HasEmbeddedPhone,
            OptInKeywords = SafeList(app.OptInKeywordsJson),
            OptOutKeywords = SafeList(app.OptOutKeywordsJson),
            HelpKeywords = SafeList(app.HelpKeywordsJson),
            app.OptInMessage,
            app.OptOutMessage,
            app.HelpMessage,
            app.EstimatedMonthlyVolume,
            app.PublicConsentPageSlug,
            app.Status,
        });
    }

    [HttpPut("applications/{companyId:int}/campaign")]
    public async Task<IActionResult> UpdateCampaign(int companyId, [FromBody] AdminUpdateCampaignDTO dto, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin) return Forbid();

        var app = await _db.CompanyTwilioCampaignApplications
            .FirstOrDefaultAsync(a => a.CompanyId == companyId, ct);
        if (app == null) return NotFound();

        // Twilio spec validations
        if ((dto.CampaignDescription ?? "").Length < 40)
            return BadRequest(new { error = "CampaignDescription must be at least 40 characters." });
        if ((dto.MessageFlow ?? "").Length < 40)
            return BadRequest(new { error = "MessageFlow must be at least 40 characters." });

        var samples = dto.MessageSamples ?? new List<string>();
        if (samples.Count < 2)
            return BadRequest(new { error = "At least 2 sample messages are required." });
        if (samples.Any(s => string.IsNullOrWhiteSpace(s) || s.Length < 20))
            return BadRequest(new { error = "Each sample message must be at least 20 characters." });

        app.UseCase = string.IsNullOrWhiteSpace(dto.UseCase) ? "LOW_VOLUME" : dto.UseCase;
        app.CampaignDescription = dto.CampaignDescription;
        app.MessageFlow = dto.MessageFlow;
        app.MessageSamplesJson = JsonSerializer.Serialize(samples);
        app.HasEmbeddedLinks = dto.HasEmbeddedLinks;
        app.HasEmbeddedPhone = dto.HasEmbeddedPhone;
        app.OptInKeywordsJson  = JsonSerializer.Serialize(dto.OptInKeywords  ?? new List<string> { "START" });
        app.OptOutKeywordsJson = JsonSerializer.Serialize(dto.OptOutKeywords ?? new List<string> { "STOP"  });
        app.HelpKeywordsJson   = JsonSerializer.Serialize(dto.HelpKeywords   ?? new List<string> { "HELP"  });
        app.OptInMessage  = dto.OptInMessage;
        app.OptOutMessage = string.IsNullOrWhiteSpace(dto.OptOutMessage)
            ? "You have successfully unsubscribed. You will no longer receive SMS messages."
            : dto.OptOutMessage!;
        app.HelpMessage = string.IsNullOrWhiteSpace(dto.HelpMessage)
            ? "Reply STOP to unsubscribe. Contact the business directly for support."
            : dto.HelpMessage!;
        if (!string.IsNullOrWhiteSpace(dto.EstimatedMonthlyVolume))
            app.EstimatedMonthlyVolume = dto.EstimatedMonthlyVolume!;

        _db.CompanyMessagingAuditLogs.Add(new CompanyMessagingAuditLog
        {
            CompanyId = companyId,
            UserId = _currentUser.UserId,
            Action = "CampaignUpdated",
            AfterJson = JsonSerializer.Serialize(new { app.UseCase, app.CampaignDescription, app.MessageFlow }),
            CreatedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    private static List<string> SafeList(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); } catch { return new(); }
    }

    // ----- Documents review -----

    [HttpPatch("documents/{id:int}/approve")]
    public async Task<IActionResult> ApproveDocument(int id, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin) return Forbid();
        var doc = await _db.CompanyTwilioDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc == null) return NotFound();

        doc.Status = "Approved";
        doc.RejectionReason = null;
        doc.ReviewedAtUtc = DateTime.UtcNow;
        doc.ReviewedByUserId = _currentUser.UserId;

        _db.CompanyMessagingAuditLogs.Add(new CompanyMessagingAuditLog
        {
            CompanyId = doc.CompanyId,
            UserId = _currentUser.UserId,
            Action = "DocumentApproved",
            AfterJson = JsonSerializer.Serialize(new { doc.Id, doc.DocumentType }),
            CreatedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    [HttpPatch("documents/{id:int}/reject")]
    public async Task<IActionResult> RejectDocument(int id, [FromBody] AdminReviewDocumentDTO dto, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin) return Forbid();
        var doc = await _db.CompanyTwilioDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc == null) return NotFound();

        doc.Status = "Rejected";
        doc.RejectionReason = dto.RejectionReason;
        doc.ReviewedAtUtc = DateTime.UtcNow;
        doc.ReviewedByUserId = _currentUser.UserId;

        _db.CompanyMessagingAuditLogs.Add(new CompanyMessagingAuditLog
        {
            CompanyId = doc.CompanyId,
            UserId = _currentUser.UserId,
            Action = "DocumentRejected",
            Notes = dto.RejectionReason,
            AfterJson = JsonSerializer.Serialize(new { doc.Id, doc.DocumentType }),
            CreatedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    // ----- Audit log -----

    // ----- Message runs (SMS + Email envelope log) -----

    [HttpGet("runs")]
    public async Task<IActionResult> ListRuns(
        [FromQuery] string? channel,    // "Sms" | "Email"
        [FromQuery] string? status,     // "Sent" | "Failed" | "Pending" | "Skipped"
        [FromQuery] string? kind,       // ConfirmationSms24h | ReminderEmail48h | OnMyWaySms | OnMyWayEmail | ReviewRequestEmail
        [FromQuery] int? companyId,
        [FromQuery] string? search,     // matches recipient phone OR email (substring)
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAdmin) return Forbid();

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var q = _db.AppointmentMessageLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(channel) && Enum.TryParse<AppointmentMessageChannel>(channel, true, out var ch))
            q = q.Where(l => l.Channel == ch);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AppointmentMessageStatus>(status, true, out var st))
            q = q.Where(l => l.Status == st);

        if (!string.IsNullOrWhiteSpace(kind) && Enum.TryParse<AppointmentMessageKind>(kind, true, out var k))
            q = q.Where(l => l.Kind == k);

        if (fromUtc.HasValue) q = q.Where(l => l.CreatedDate >= fromUtc.Value);
        if (toUtc.HasValue)   q = q.Where(l => l.CreatedDate <= toUtc.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(l =>
                (l.RecipientEmail != null && EF.Functions.ILike(l.RecipientEmail, $"%{s}%")) ||
                (l.RecipientPhoneE164 != null && EF.Functions.ILike(l.RecipientPhoneE164, $"%{s}%")) ||
                (l.Subject != null && EF.Functions.ILike(l.Subject, $"%{s}%")));
        }

        // Resolve companyId via the linked Appointment (cheap join — companies are small)
        var apptQuery = _db.Appointments.AsNoTracking()
            .Select(a => new { a.Id, a.CompanyId });

        var joined = q.Join(apptQuery, l => l.AppointmentId, a => a.Id, (l, a) => new { Log = l, a.CompanyId });

        if (companyId.HasValue)
            joined = joined.Where(x => x.CompanyId == companyId.Value);

        var total = await joined.CountAsync(ct);

        var rows = await joined
            .OrderByDescending(x => x.Log.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var companyIds = rows.Select(r => r.CompanyId).Distinct().ToList();
        var companies = await _db.Companies.AsNoTracking()
            .Where(c => companyIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var items = rows.Select(r => new MessageRunListItemDTO
        {
            Id = r.Log.Id,
            AppointmentId = r.Log.AppointmentId,
            CompanyId = r.CompanyId,
            CompanyName = companies.TryGetValue(r.CompanyId, out var n) ? n : null,
            Channel = r.Log.Channel.ToString(),
            Kind = r.Log.Kind.ToString(),
            Status = r.Log.Status.ToString(),
            Attempt = r.Log.Attempt,
            RecipientEmail = r.Log.RecipientEmail,
            RecipientPhoneE164 = r.Log.RecipientPhoneE164,
            SenderPhoneE164 = r.Log.SenderPhoneE164,
            SenderSource = r.Log.SenderSource,
            Subject = r.Log.Subject,
            Provider = r.Log.Provider,
            ProviderStatus = r.Log.ProviderStatus,
            LastError = r.Log.LastError,
            WasBlockedByMessagingPolicy = r.Log.WasBlockedByMessagingPolicy,
            MessagingBlockReason = r.Log.MessagingBlockReason,
            ScheduledForUtc = r.Log.ScheduledForUtc,
            SentAtUtc = r.Log.SentAtUtc,
            CreatedDate = r.Log.CreatedDate,
        }).ToList();

        return Ok(new MessageRunsPageDTO
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = items,
        });
    }

    [HttpGet("runs/stats")]
    public async Task<IActionResult> RunStats(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int? companyId,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAdmin) return Forbid();

        var to   = toUtc   ?? DateTime.UtcNow;
        var from = fromUtc ?? to.AddDays(-7);

        var q = _db.AppointmentMessageLogs.AsNoTracking()
            .Where(l => l.CreatedDate >= from && l.CreatedDate <= to);

        if (companyId.HasValue)
        {
            var apptIds = _db.Appointments.AsNoTracking()
                .Where(a => a.CompanyId == companyId.Value)
                .Select(a => a.Id);
            q = q.Where(l => apptIds.Contains(l.AppointmentId));
        }

        var data = await q
            .GroupBy(l => new { l.Channel, l.Status })
            .Select(g => new { g.Key.Channel, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        int CountFor(AppointmentMessageChannel? c, AppointmentMessageStatus? s)
        {
            return data
                .Where(d => (c == null || d.Channel == c) && (s == null || d.Status == s))
                .Sum(d => d.Count);
        }

        var stats = new MessageRunStatsDTO
        {
            FromUtc = from,
            ToUtc = to,
            Total   = CountFor(null, null),
            Sent    = CountFor(null, AppointmentMessageStatus.Sent),
            Failed  = CountFor(null, AppointmentMessageStatus.Failed),
            Pending = CountFor(null, AppointmentMessageStatus.Pending),
            Skipped = CountFor(null, AppointmentMessageStatus.Skipped),

            SmsTotal  = CountFor(AppointmentMessageChannel.Sms, null),
            SmsSent   = CountFor(AppointmentMessageChannel.Sms, AppointmentMessageStatus.Sent),
            SmsFailed = CountFor(AppointmentMessageChannel.Sms, AppointmentMessageStatus.Failed),

            EmailTotal  = CountFor(AppointmentMessageChannel.Email, null),
            EmailSent   = CountFor(AppointmentMessageChannel.Email, AppointmentMessageStatus.Sent),
            EmailFailed = CountFor(AppointmentMessageChannel.Email, AppointmentMessageStatus.Failed),
        };
        return Ok(stats);
    }

    // ----- Documents review (cross-company, grouped) -----

    [HttpGet("documents")]
    public async Task<IActionResult> ListAllDocuments(
        [FromQuery] string? status,        // "Pending" | "Approved" | "Rejected"
        [FromQuery] int? companyId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,     // pageSize is per-company (#groups)
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAdmin) return Forbid();
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var docsQ = _db.CompanyTwilioDocuments.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && status != "all")
            docsQ = docsQ.Where(d => d.Status == status);
        if (companyId.HasValue)
            docsQ = docsQ.Where(d => d.CompanyId == companyId.Value);

        // Pull all docs that match the filter (we group on the server; keep it sane with a hard cap)
        var allDocs = await docsQ
            .OrderByDescending(d => d.CreatedDate)
            .Take(2000)
            .ToListAsync(ct);

        if (allDocs.Count == 0)
        {
            return Ok(new AdminDocumentsPageDTO { TotalCompanies = 0, TotalDocuments = 0, PendingDocuments = 0 });
        }

        var companyIds = allDocs.Select(d => d.CompanyId).Distinct().ToList();
        var companies = await _db.Companies.AsNoTracking()
            .Where(c => companyIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var profiles = await _db.CompanyMessagingProfiles.AsNoTracking()
            .Where(p => companyIds.Contains(p.CompanyId))
            .Select(p => new { p.CompanyId, p.Status })
            .ToDictionaryAsync(x => x.CompanyId, x => x.Status, ct);

        // Pull the campaign applications (1 per company) so we can render the Business profile
        var applications = await _db.CompanyTwilioCampaignApplications.AsNoTracking()
            .Where(a => companyIds.Contains(a.CompanyId))
            .ToDictionaryAsync(a => a.CompanyId, ct);

                var reviewerIds = allDocs.Where(d => d.ReviewedByUserId.HasValue)
            .Select(d => d.ReviewedByUserId!.Value).Distinct().ToList();
        var reviewers = await _db.Users.AsNoTracking()
            .Where(u => reviewerIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var groups = allDocs
            .GroupBy(d => d.CompanyId)
            .Select(g =>
            {
                var compName = companies.TryGetValue(g.Key, out var n) ? n : $"Company {g.Key}";
                var profileStatus = profiles.TryGetValue(g.Key, out var ps) ? ps : "";
                var docs = g.Select(d => new AdminDocumentItemDTO
                {
                    Id = d.Id,
                    CompanyId = d.CompanyId,
                    CampaignApplicationId = d.CampaignApplicationId,
                    DocumentType = d.DocumentType,
                    FileUrl = _s3.CreateDownloadUrl(d.FileUrl) ?? d.FileUrl, // signed URL
                    OriginalFileName = d.OriginalFileName,
                    ContentType = d.ContentType,
                    Status = d.Status,
                    RejectionReason = d.RejectionReason,
                    ReviewedByUserId = d.ReviewedByUserId,
                    ReviewedByName = d.ReviewedByUserId.HasValue && reviewers.TryGetValue(d.ReviewedByUserId.Value, out var rn) ? rn : null,
                    ReviewedAtUtc = d.ReviewedAtUtc,
                    CreatedDate = d.CreatedDate,
                }).ToList();

                applications.TryGetValue(g.Key, out var appEntity);
                CompanyTwilioCampaignApplicationDTO? appDto = null;
                if (appEntity != null)
                {
                    appDto = new CompanyTwilioCampaignApplicationDTO
                    {
                        Id = appEntity.Id,
                        CompanyId = appEntity.CompanyId,
                        LegalBusinessName = appEntity.LegalBusinessName,
                        DbaName = appEntity.DbaName,
                        Ein = appEntity.Ein,
                        BusinessType = appEntity.BusinessType,
                        BusinessWebsiteUrl = appEntity.BusinessWebsiteUrl,
                        BusinessAddressLine1 = appEntity.BusinessAddressLine1,
                        BusinessAddressLine2 = appEntity.BusinessAddressLine2,
                        BusinessCity = appEntity.BusinessCity,
                        BusinessState = appEntity.BusinessState,
                        BusinessPostalCode = appEntity.BusinessPostalCode,
                        BusinessCountry = appEntity.BusinessCountry,
                        ContactFirstName = appEntity.ContactFirstName,
                        ContactLastName = appEntity.ContactLastName,
                        ContactEmail = appEntity.ContactEmail,
                        ContactPhoneE164 = appEntity.ContactPhoneE164,
                        UseCase = appEntity.UseCase,
                        CampaignDescription = appEntity.CampaignDescription,
                        MessageFlow = appEntity.MessageFlow,
                        TermsUrl = appEntity.TermsUrl,
                        PrivacyPolicyUrl = appEntity.PrivacyPolicyUrl,
                        EstimatedMonthlyVolume = appEntity.EstimatedMonthlyVolume,
                        PublicConsentPageSlug = appEntity.PublicConsentPageSlug,
                        Status = appEntity.Status,
                        AdminReviewNotes = appEntity.AdminReviewNotes,
                        SubmittedAtUtc = appEntity.SubmittedAtUtc,
                        CreatedDate = appEntity.CreatedDate,
                        UpdatedDate = appEntity.UpdatedDate,
                    };
                }

                // Admin always sees the landing URL when a slug exists, regardless of status.
                // The PUBLIC landing endpoint still returns 404 while the application is in Draft,
                // so this is admin-only visibility — useful to preview / share early.
                string? landingUrl = null;
                if (appEntity != null
                    && !string.IsNullOrWhiteSpace(appEntity.PublicConsentPageSlug))
                {
                    landingUrl = $"{PublicAppBaseUrl}/sms-consent?slug={appEntity.PublicConsentPageSlug}";
                }

                return new AdminDocumentsCompanyGroupDTO
                {
                    CompanyId = g.Key,
                    CompanyName = compName,
                    ProfileStatus = profileStatus,
                    ApplicationStatus = appEntity?.Status ?? "",
                    LandingPageUrl = landingUrl,
                    Total = docs.Count,
                    Pending = docs.Count(x => x.Status == "Pending"),
                    Approved = docs.Count(x => x.Status == "Approved"),
                    Rejected = docs.Count(x => x.Status == "Rejected"),
                    Application = appDto,
                    Documents = docs,
                };
            })
            .OrderByDescending(g => g.Pending)
            .ThenBy(g => g.CompanyName)
            .ToList();

        var totalCompanies = groups.Count;
        var paged = groups.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Ok(new AdminDocumentsPageDTO
        {
            TotalCompanies = totalCompanies,
            TotalDocuments = allDocs.Count,
            PendingDocuments = allDocs.Count(d => d.Status == "Pending"),
            Groups = paged,
        });
    }

    // ----- Unified compliance audit feed (cross-company timeline) -----

    [HttpGet("compliance-audit")]
    public async Task<IActionResult> ListComplianceAudit(
        [FromQuery] string? action,         // exact Action match (e.g. "TrialReminderD1Sent")
        [FromQuery] int? companyId,
        [FromQuery] string? search,         // matches Notes substring
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAdmin) return Forbid();
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var q = _db.CompanyMessagingAuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
            q = q.Where(l => l.Action == action);
        if (companyId.HasValue)
            q = q.Where(l => l.CompanyId == companyId.Value);
        if (fromUtc.HasValue) q = q.Where(l => l.CreatedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue)   q = q.Where(l => l.CreatedAtUtc <= toUtc.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(l => l.Notes != null && EF.Functions.ILike(l.Notes, $"%{s}%"));
        }

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(l => l.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var companyIds = rows.Select(r => r.CompanyId).Distinct().ToList();
        var userIds = rows.Where(r => r.UserId.HasValue).Select(r => r.UserId!.Value).Distinct().ToList();

        var companies = await _db.Companies.AsNoTracking()
            .Where(c => companyIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var users = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var items = rows.Select(l => new ComplianceAuditItemDTO
        {
            Id = l.Id,
            CompanyId = l.CompanyId,
            CompanyName = companies.TryGetValue(l.CompanyId, out var cn) ? cn : null,
            UserId = l.UserId,
            UserName = l.UserId.HasValue && users.TryGetValue(l.UserId.Value, out var un) ? un : null,
            Action = l.Action,
            Notes = l.Notes,
            AfterJson = l.AfterJson,
            BeforeJson = l.BeforeJson,
            CreatedAtUtc = l.CreatedAtUtc,
        }).ToList();

        return Ok(new ComplianceAuditPageDTO
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = items,
        });
    }

    // ----- Today plan (24h SMS + 48h Email scheduled to fire today) -----

    [HttpGet("today")]
    public async Task<IActionResult> ListToday(CancellationToken ct)
    {
        if (!_currentUser.IsAdmin) return Forbid();
        var plan = await _today.ListTodayAsync(ct);
        return Ok(plan);
    }

    /// <summary>
    /// Force-dispatches every Pending reminder due "today" right now, instead of
    /// waiting for the next hosted-service tick.
    /// </summary>
    [HttpPost("today/run-now")]
    public async Task<IActionResult> RunToday(CancellationToken ct)
    {
        if (!_currentUser.IsAdmin) return Forbid();
        var result = await _today.RunTodayAsync(ct);
        return Ok(result);
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> AuditLogs([FromQuery] int companyId, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin) return Forbid();
        var logs = await _db.CompanyMessagingAuditLogs.AsNoTracking()
            .Where(l => l.CompanyId == companyId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(500)
            .Select(l => new AdminAuditLogDTO
            {
                Id = l.Id,
                CompanyId = l.CompanyId,
                UserId = l.UserId,
                Action = l.Action,
                Notes = l.Notes,
                CreatedAtUtc = l.CreatedAtUtc,
            })
            .ToListAsync(ct);
        return Ok(logs);
    }
}
