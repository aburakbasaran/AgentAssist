namespace AgentAssist.Evaluation.Tests.DevCloud;

/// <summary>
/// Holds the most recent LLM call observed by <see cref="TranscriptCapturingChatClient"/> (test assembly only).
/// </summary>
public sealed class ChatTranscriptCollector
{
    public ChatTranscriptRecord? Last { get; private set; }

    public void Record(ChatTranscriptRecord record) => Last = record;

    public void Clear() => Last = null;
}

public sealed record ChatTranscriptRecord(
    bool LlmInvoked,
    string? UserMessageSentToModel,
    string? RawModelResponseText,
    DateTimeOffset CapturedAtUtc);
