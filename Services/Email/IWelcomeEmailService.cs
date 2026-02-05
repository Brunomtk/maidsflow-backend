using System.Threading;
using System.Threading.Tasks;

namespace Services.Email;

public interface IWelcomeEmailService
{
    Task<SendWelcomeEmailResult> SendWelcomeEmailAsync(int userId, string? loginUrl, CancellationToken ct = default);
}

public sealed record SendWelcomeEmailResult(
    int UserId,
    string ToEmail,
    bool EmailSent,
    int? ProviderStatusCode,
    string? ProviderResponse
);
