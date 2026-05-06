using System.Text.Json;

namespace AgentAssist.Application.Ai;

/// <summary>
/// Strict JSON parser for the <see cref="AssistantAnswerEnvelope"/> contract. The model is instructed to return JSON only; the <c>JsonUnmappedMemberHandling.Disallow</c> attribute on the envelope causes any extra members to surface as a parse failure that the handler maps to a structured refusal.
/// </summary>
public static class ChatResponseParser
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = false
    };

    /// <summary>
    /// Attempts to parse the chat response text into a strict envelope. Returns <see langword="false"/> when the model output is not a JSON object that matches the schema.
    /// </summary>
    /// <param name="responseText">The raw chat response text, optionally wrapped in <c>```json ... ```</c> markdown fences.</param>
    /// <param name="envelope">When successful, the parsed envelope.</param>
    /// <returns><see langword="true"/> when parsing succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? responseText, out AssistantAnswerEnvelope? envelope)
    {
        envelope = null;
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return false;
        }

        var json = ExtractJsonObject(responseText);
        if (json is null)
        {
            return false;
        }

        try
        {
            envelope = JsonSerializer.Deserialize<AssistantAnswerEnvelope>(json, Options);
        }
        catch (JsonException)
        {
            return false;
        }

        if (envelope is null)
        {
            return false;
        }

        if (envelope.Citations is null || envelope.AnswerText is null)
        {
            envelope = null;
            return false;
        }

        return true;
    }

    private static string? ExtractJsonObject(string responseText)
    {
        var trimmed = responseText.Trim();

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstBrace = trimmed.IndexOf('{', StringComparison.Ordinal);
            var lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace < 0 || lastBrace <= firstBrace)
            {
                return null;
            }

            return trimmed[firstBrace..(lastBrace + 1)];
        }

        if (trimmed.Length is 0 || trimmed[0] is not '{')
        {
            return null;
        }

        return trimmed;
    }
}
