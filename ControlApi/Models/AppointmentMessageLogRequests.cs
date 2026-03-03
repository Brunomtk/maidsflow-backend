using System;

namespace ControlApi.Models;

/// <summary>
/// Request usado pelo n8n para criar um log antes do envio.
/// Os campos Kind/Channel/Status aceitam tanto nome ("ConfirmationSms24h") quanto número ("1").
/// </summary>
public class CreateAppointmentMessageLogRequest
{
    public string Kind { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";

    public DateTime? ScheduledForUtc { get; set; }
    public DateTime? OccurrenceStartUtc { get; set; }
    public DateTime? OccurrenceEndUtc { get; set; }

    public string? RecipientEmail { get; set; }
    public string? RecipientPhoneE164 { get; set; }

    public string? Subject { get; set; }
    public string? BodyText { get; set; }
    public string? TemplateKey { get; set; }
    public string? PayloadJson { get; set; }

    public string? RequestedByRole { get; set; } = "System";
}

/// <summary>
/// Request usado pelo n8n para atualizar um log após o envio.
/// </summary>
public class UpdateAppointmentMessageLogRequest
{
    public string? Status { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? ProviderStatus { get; set; }
    public string? LastError { get; set; }
    public string? LastErrorRaw { get; set; }
}
