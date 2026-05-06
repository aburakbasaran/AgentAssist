using System.Text.RegularExpressions;

namespace AgentAssist.Application.Auditing;

/// <summary>
/// Redacts long numeric runs that may correspond to identity, payment card, or phone numbers from text destined for audit, logs, or telemetry. The redactor is deliberately conservative: it never modifies known short tokens.
/// </summary>
public static partial class SensitiveNumberRedactor
{
    private const string Mask = "[redacted-number]";

    /// <summary>
    /// Returns a copy of the input with sensitive number patterns replaced by a non-reversible mask token.
    /// </summary>
    /// <param name="value">The input value.</param>
    /// <returns>The redacted value.</returns>
    public static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        var redacted = SixteenDigitPattern().Replace(value, Mask);
        redacted = ElevenDigitPattern().Replace(redacted, Mask);
        return redacted;
    }

    [GeneratedRegex(@"\b\d{16}\b", RegexOptions.CultureInvariant)]
    private static partial Regex SixteenDigitPattern();

    [GeneratedRegex(@"\b\d{11}\b", RegexOptions.CultureInvariant)]
    private static partial Regex ElevenDigitPattern();
}
