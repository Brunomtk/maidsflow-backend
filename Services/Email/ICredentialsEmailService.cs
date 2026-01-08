using System.Threading;
using System.Threading.Tasks;

namespace Services.Email;

public interface ICredentialsEmailService
{
    Task<SendCredentialsResult> SendUserCredentialsAsync(int userId, bool generateNewPassword, string? loginUrl, CancellationToken ct = default);
}

public sealed record SendCredentialsResult(
    int UserId,
    string ToEmail,
    bool PasswordRegenerated,
    string? GeneratedPassword,
    bool EmailSent,
    int? ProviderStatusCode,
    string? ProviderResponse
);
