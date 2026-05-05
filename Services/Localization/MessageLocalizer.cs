using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Services.Localization;

/// <inheritdoc />
public class MessageLocalizer : IMessageLocalizer
{
    private readonly LocalizationResources _resources;
    private static readonly Regex PlaceholderRx = new(@"\{(\w+)\}", RegexOptions.Compiled);

    public MessageLocalizer(LocalizationResources resources)
    {
        _resources = resources;
    }

    public string Get(string key, string language, object? vars = null)
    {
        var dict = vars == null ? null : ToDict(vars);
        return Get(key, language, dict);
    }

    public string Get(string key, string language, IReadOnlyDictionary<string, object?>? vars)
    {
        if (string.IsNullOrWhiteSpace(key)) return key ?? string.Empty;

        var lang = SupportedLanguages.Normalize(language);

        // Try requested language first, then fall back to default ("en").
        var template = _resources.Lookup(lang, key)
                       ?? _resources.Lookup(SupportedLanguages.Default, key)
                       ?? key; // mirror frontend behavior: return the key as a last-resort marker.

        return Interpolate(template, vars);
    }

    public bool HasKey(string key, string language)
    {
        var lang = SupportedLanguages.Normalize(language);
        return _resources.Lookup(lang, key) != null
            || _resources.Lookup(SupportedLanguages.Default, key) != null;
    }

    // ---------- helpers ----------

    private static string Interpolate(string template, IReadOnlyDictionary<string, object?>? vars)
    {
        if (vars == null || vars.Count == 0) return template;

        return PlaceholderRx.Replace(template, m =>
        {
            var name = m.Groups[1].Value;
            if (vars.TryGetValue(name, out var v))
            {
                return v?.ToString() ?? string.Empty;
            }
            // Leave the placeholder if not provided (caller bug) — same behavior as frontend.
            return m.Value;
        });
    }

    private static IReadOnlyDictionary<string, object?> ToDict(object source)
    {
        if (source is IReadOnlyDictionary<string, object?> rod) return rod;
        if (source is IDictionary<string, object?> d)
            return new Dictionary<string, object?>(d);

        // Anonymous types: reflect over public properties
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        var t = source.GetType();
        foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            try { dict[prop.Name] = prop.GetValue(source); } catch { /* ignore */ }
        }
        return dict;
    }
}
