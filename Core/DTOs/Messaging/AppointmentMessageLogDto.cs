using System;

namespace Core.DTOs.Messaging;

public class AppointmentMessageLogDto
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }

    // Keep Kind as-is (matches backend enum values: 1,2,10,11)
    public int Kind { get; set; }

    // Frontend expects 0-based: 0=Email, 1=Sms
    public int Channel { get; set; }

    // Frontend expects 0-based: 0=Pending, 1=Sent, 2=Failed, 3=Skipped
    public int Status { get; set; }

    public DateTime? ScheduledForUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public int Attempt { get; set; }

    public int? RequestedByUserId { get; set; }
    public string? RequestedByRole { get; set; }

    public string? RecipientEmail { get; set; }
    public string? RecipientPhoneE164 { get; set; }

    public string? Subject { get; set; }
    public string? BodyText { get; set; }

    public string? TemplateKey { get; set; }
    public string? PayloadJson { get; set; }

    public string? Provider { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? ProviderStatus { get; set; }

    public string? LastError { get; set; }
    public string? LastErrorRaw { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }

    public Guid? SeriesId { get; set; }
    public DateTime? OccurrenceStartUtc { get; set; }
    public DateTime? OccurrenceEndUtc { get; set; }
}
