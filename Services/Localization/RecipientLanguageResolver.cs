using System.Threading;
using System.Threading.Tasks;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Services.Localization;

/// <inheritdoc />
public class RecipientLanguageResolver : IRecipientLanguageResolver
{
    private readonly DbContextClass _db;
    private readonly ILogger<RecipientLanguageResolver> _logger;

    public RecipientLanguageResolver(DbContextClass db, ILogger<RecipientLanguageResolver> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<string> ForCustomerAsync(int customerId, CancellationToken ct = default)
    {
        try
        {
            // Pull both Customer.Language and Company.Language in a single query.
            var row = await _db.Customers
                .AsNoTracking()
                .Where(c => c.Id == customerId)
                .Select(c => new { c.Language, CompanyLanguage = c.Company != null ? c.Company.Language : null })
                .FirstOrDefaultAsync(ct);

            if (row == null) return SupportedLanguages.Default;
            return SupportedLanguages.Resolve(row.Language, row.CompanyLanguage);
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "[Localization] Failed to resolve language for customerId={CustomerId}", customerId);
            return SupportedLanguages.Default;
        }
    }

    public async Task<string> ForUserAsync(int userId, CancellationToken ct = default)
    {
        try
        {
            // User has Language. If null and the user is bound to a company, fall back to Company.Language.
            var row = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Language, u.CompanyId })
                .FirstOrDefaultAsync(ct);

            if (row == null) return SupportedLanguages.Default;

            string? companyLanguage = null;
            if (string.IsNullOrWhiteSpace(row.Language) && row.CompanyId.HasValue)
            {
                companyLanguage = await _db.Companies
                    .AsNoTracking()
                    .Where(c => c.Id == row.CompanyId.Value)
                    .Select(c => c.Language)
                    .FirstOrDefaultAsync(ct);
            }

            return SupportedLanguages.Resolve(row.Language, companyLanguage);
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "[Localization] Failed to resolve language for userId={UserId}", userId);
            return SupportedLanguages.Default;
        }
    }

    public async Task<string> ForCompanyAsync(int companyId, CancellationToken ct = default)
    {
        try
        {
            var lang = await _db.Companies
                .AsNoTracking()
                .Where(c => c.Id == companyId)
                .Select(c => c.Language)
                .FirstOrDefaultAsync(ct);

            return SupportedLanguages.Resolve(lang);
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "[Localization] Failed to resolve language for companyId={CompanyId}", companyId);
            return SupportedLanguages.Default;
        }
    }

    public string Resolve(string? recipientLanguage, string? companyLanguage = null)
        => SupportedLanguages.Resolve(recipientLanguage, companyLanguage);
}
