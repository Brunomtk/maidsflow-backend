using System.Threading;
using System.Threading.Tasks;

namespace Services.Email;

public interface ICredentialsEmailService
{
    Task<SendCredentialsResult> SendUserCredentialsAsync(int userId, bool generateNewPassword, string? loginUrl, CancellationToken ct = default);

    /// <summary>
    /// Sends a notice email to the user that their password was changed.
    /// </summary>
    Task<SendPasswordChangedNoticeResult> SendPasswordChangedNoticeAsync(int userId, string? loginUrl, CancellationToken ct = default);
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

public sealed record SendPasswordChangedNoticeResult(
    int UserId,
    string ToEmail,
    bool EmailSent,
    int? ProviderStatusCode,
    string? ProviderResponse
);
