using System.Text.RegularExpressions;

namespace Services.Integrations.Twilio;

public static class PhoneNumberUtils
{
    private static readonly Regex NonDigits = new("[^0-9]", RegexOptions.Compiled);

    /// <summary>
    /// Best-effort normalization to E.164.
    ///
    /// Rules (conservative):
    /// - Keeps '+' if already provided.
    /// - Converts leading '00' to '+'.
    /// - If only digits:
    ///   - 10 digits => assumes US/CA (+1)
    ///   - 11 digits starting with '1' => +{digits}
    ///   - 12-13 digits starting with '55' => +{digits} (Brazil)
    ///   - Otherwise: requires the stored value to already include country code.
    /// </summary>
    public static string NormalizeToE164OrThrow(string input, string paramName = "phone")
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new TwilioValidationException("Phone number is required.", paramName);

        var raw = input.Trim();

        if (raw.StartsWith("+"))
        {
            var digits = NonDigits.Replace(raw, "");
            if (digits.Length < 8 || digits.Length > 15)
                throw new TwilioValidationException("Phone number must be a valid E.164 number (e.g., +18134698765).", paramName);
            return "+" + digits;
        }

        if (raw.StartsWith("00"))
        {
            var digits = NonDigits.Replace(raw, "");
            digits = digits.StartsWith("00") ? digits[2..] : digits;
            if (digits.Length < 8 || digits.Length > 15)
                throw new TwilioValidationException("Phone number must be a valid international number.", paramName);
            return "+" + digits;
        }

        var onlyDigits = NonDigits.Replace(raw, "");

        // US/CA common cases
        if (onlyDigits.Length == 10)
            return "+1" + onlyDigits;

        if (onlyDigits.Length == 11 && onlyDigits.StartsWith("1"))
            return "+" + onlyDigits;

        // Brazil common cases (55 + DDD + number)
        if ((onlyDigits.Length == 12 || onlyDigits.Length == 13) && onlyDigits.StartsWith("55"))
            return "+" + onlyDigits;

        // If it's already with country code but missing '+', try prefixing.
        if (onlyDigits.Length >= 8 && onlyDigits.Length <= 15)
            return "+" + onlyDigits;

        throw new TwilioValidationException("Phone number must be in E.164 format (e.g., +18134698765).", paramName);
    }
}
