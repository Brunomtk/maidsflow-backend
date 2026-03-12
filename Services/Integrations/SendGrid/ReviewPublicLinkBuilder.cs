using System;

namespace Services.Integrations.SendGrid;

public static class ReviewPublicLinkBuilder
{
    public static string Build(string baseUrl, Guid token)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL cannot be empty.", nameof(baseUrl));

        var trimmed = baseUrl.Trim();
        var encodedToken = Uri.EscapeDataString(token.ToString());

        if (trimmed.Contains("{token}", StringComparison.OrdinalIgnoreCase))
            return trimmed.Replace("{token}", encodedToken, StringComparison.OrdinalIgnoreCase);

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute))
        {
            var separator = string.IsNullOrWhiteSpace(absolute.Query) ? "?" : "&";
            return $"{trimmed}{separator}token={encodedToken}";
        }

        var normalized = trimmed.TrimEnd('/');
        var hasQuery = normalized.Contains('?', StringComparison.Ordinal);
        return hasQuery
            ? $"{normalized}&token={encodedToken}"
            : $"{normalized}?token={encodedToken}";
    }
}
