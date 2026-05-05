using System.Threading;
using System.Threading.Tasks;

namespace Services.Localization;

/// <summary>
/// Resolves the preferred language for outbound communication based on the recipient.
///
/// Cascade order:
///   1. Per-recipient field (Customer.Language / User.Language)
///   2. Company.Language (the company they belong to)
///   3. <see cref="SupportedLanguages.Default"/> ("en")
///
/// Used by SMS / Email / PDF / Push generators to pick the right resource bundle.
/// </summary>
public interface IRecipientLanguageResolver
{
    /// <summary>Resolves language for a Customer (recipient of appointment SMS, review-request email, etc).</summary>
    Task<string> ForCustomerAsync(int customerId, CancellationToken ct = default);

    /// <summary>Resolves language for a User (recipient of credentials email, password reset, push notifications, etc).</summary>
    Task<string> ForUserAsync(int userId, CancellationToken ct = default);

    /// <summary>Resolves language for a Company (recipient of monthly report email/PDF, plan invoice, etc).</summary>
    Task<string> ForCompanyAsync(int companyId, CancellationToken ct = default);

    /// <summary>
    /// Direct synchronous fallback when caller already has the values in memory.
    /// Cascades: <paramref name="recipientLanguage"/> → <paramref name="companyLanguage"/> → default.
    /// </summary>
    string Resolve(string? recipientLanguage, string? companyLanguage = null);
}
