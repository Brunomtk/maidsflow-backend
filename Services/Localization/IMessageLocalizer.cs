using System.Collections.Generic;

namespace Services.Localization;

/// <summary>
/// Counterpart of the frontend's <c>t()</c>: returns a translated string for a given key
/// in the requested language, with optional <c>{placeholder}</c> interpolation.
///
/// Resource keys live in <see cref="LocalizationResources"/> (one big dictionary per language).
/// Lookup falls back to "en" if the key is missing in the requested language.
/// If the key does not exist anywhere, returns the key itself (mirrors the frontend fallback).
/// </summary>
public interface IMessageLocalizer
{
    /// <summary>
    /// Translates a key. <paramref name="vars"/> values replace <c>{name}</c>-style placeholders.
    /// </summary>
    /// <example>
    /// <code>
    /// _loc.Get("sms.appointmentReminder.body", "pt-BR", new { name = "Maria", time = "14:00" })
    /// // → "Olá Maria, lembrete: seu serviço começa em 30 minutos (14:00)."
    /// </code>
    /// </example>
    string Get(string key, string language, object? vars = null);

    /// <summary>
    /// Same as <see cref="Get(string, string, object?)"/> but accepts a dictionary of variables.
    /// </summary>
    string Get(string key, string language, IReadOnlyDictionary<string, object?>? vars);

    /// <summary>True if the key exists in the requested language (or in the fallback "en").</summary>
    bool HasKey(string key, string language);
}
