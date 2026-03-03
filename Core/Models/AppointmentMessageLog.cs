using Core.Enums.Messaging;

namespace Core.Models;

public class AppointmentMessageLog : BaseModel
{
    public int AppointmentId { get; set; }

    // Recurrence / occurrence context
    // For recurring appointments, a single Appointment record can represent an entire series.
    // These fields allow logging per occurrence (instance) being viewed/sent.
    public Guid? SeriesId { get; set; }
    public DateTime? OccurrenceStartUtc { get; set; }
    public DateTime? OccurrenceEndUtc { get; set; }

    public AppointmentMessageKind Kind { get; set; }
    public AppointmentMessageChannel Channel { get; set; }
    public AppointmentMessageStatus Status { get; set; }

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

    public string Provider { get; set; } = string.Empty;
    public string? ProviderMessageId { get; set; }
    public string? ProviderStatus { get; set; }

    public string? LastError { get; set; }
    public string? LastErrorRaw { get; set; }
}
