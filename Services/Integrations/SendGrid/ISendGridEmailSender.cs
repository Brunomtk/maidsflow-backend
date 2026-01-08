using System.Threading;
using System.Threading.Tasks;

namespace Services.Integrations.SendGrid;

public interface ISendGridEmailSender
{
    Task<SendGridSendResult> SendAsync(SendGridEmailMessage message, CancellationToken ct = default);
}

public sealed record SendGridEmailMessage(
    string ToEmail,
    string Subject,
    string PlainText,
    string Html,
    string? ToName = null
);

public sealed record SendGridSendResult(
    bool Ok,
    int StatusCode,
    string? ResponseBody = null,
    string? Error = null
);
