using System.Text.Json.Serialization;

namespace AgentAssist.Application.Ai;

/// <summary>
/// Strict structured envelope returned by the chat model. The schema is enforced by <see cref="ChatResponseParser"/> with <see cref="JsonUnmappedMemberHandling.Disallow"/>; unknown fields trigger a structured refusal.
/// </summary>
/// <param name="AnswerText">The answer text or, when refused, the refusal explanation.</param>
/// <param name="Citations">Chunk identifiers the model used to ground its answer; must be a subset of the retrieved chunks.</param>
/// <param name="Confidence">Optional self-reported confidence (<c>"Low"</c>, <c>"Medium"</c>, or <c>"High"</c>).</param>
/// <param name="Refused">Whether the model self-refused (insufficient grounding).</param>
/// <param name="RefusalReason">Refusal reason when <paramref name="Refused"/> is <see langword="true"/>.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssistantAnswerEnvelope(
    [property: JsonPropertyName("answerText")] string AnswerText,
    [property: JsonPropertyName("citations")] IReadOnlyList<string> Citations,
    [property: JsonPropertyName("confidence")] string? Confidence,
    [property: JsonPropertyName("refused")] bool Refused,
    [property: JsonPropertyName("refusalReason")] string? RefusalReason);
