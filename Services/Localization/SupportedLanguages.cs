using System;

namespace Services.Localization;

/// <summary>
/// Canonical list of languages supported across MaidsFlow outbound communication
/// (SMS, email, PDF, push). Mirrors the frontend's <c>APP_LANGUAGES</c>.
///
/// Format follows BCP-47-ish tags. The default fallback is <c>"en"</c>.
/// </summary>
public static class SupportedLanguages
{
    public const string En = "en";
    public const string PtBr = "pt-BR";
    public const string Es = "es";
    public const string Fr = "fr";

    public const string Default = En;

    public static readonly string[] All = new[] { En, PtBr, Es, Fr };

    /// <summary>
    /// Normalizes a free-form language tag to one of the supported canonical values.
    /// Handles variants like "pt", "pt-br", "pt_BR", "en-US", "es-MX", "fr-CA".
    /// Returns <see cref="Default"/> if input is null/empty/unknown.
    /// </summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Default;

        var v = raw.Trim().Replace('_', '-').ToLowerInvariant();

        if (v == "pt" || v == "pt-br" || v.StartsWith("pt-", StringComparison.Ordinal)) return PtBr;
        if (v == "es" || v.StartsWith("es-", StringComparison.Ordinal)) return Es;
        if (v == "fr" || v.StartsWith("fr-", StringComparison.Ordinal)) return Fr;
        if (v == "en" || v.StartsWith("en-", StringComparison.Ordinal)) return En;

        return Default;
    }

    /// <summary>
    /// Best-effort cascade: prefers <paramref name="primary"/>, falls back to
    /// <paramref name="secondary"/>, then to <paramref name="tertiary"/>, then to <see cref="Default"/>.
    /// </summary>
    public static string Resolve(string? primary, string? secondary = null, string? tertiary = null)
    {
        if (!string.IsNullOrWhiteSpace(primary)) return Normalize(primary);
        if (!string.IsNullOrWhiteSpace(secondary)) return Normalize(secondary);
        if (!string.IsNullOrWhiteSpace(tertiary)) return Normalize(tertiary);
        return Default;
    }
}
