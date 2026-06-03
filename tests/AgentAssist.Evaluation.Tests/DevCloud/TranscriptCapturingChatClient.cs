using Microsoft.Extensions.AI;

namespace AgentAssist.Evaluation.Tests.DevCloud;

/// <summary>
/// Observer decorator for <see cref="IChatClient"/> used only in evaluation tests. Does not alter prompts or responses.
/// </summary>
public sealed class TranscriptCapturingChatClient(IChatClient inner, ChatTranscriptCollector collector) : IChatClient
{
    /// <summary>Underlying client (e.g. for judge LLM in Layer 2 without overwriting the producer transcript).</summary>
    public IChatClient Inner => inner;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var userMessage = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text;
        return InvokeAndCaptureAsync(
            () => inner.GetResponseAsync(messages, options, cancellationToken),
            userMessage);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var userMessage = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text;
        var response = await inner.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        collector.Record(new ChatTranscriptRecord(
            LlmInvoked: true,
            UserMessageSentToModel: userMessage,
            RawModelResponseText: response.Text,
            CapturedAtUtc: DateTimeOffset.UtcNow));
        yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        inner.GetService(serviceType, serviceKey);

    public void Dispose() => inner.Dispose();

    private async Task<ChatResponse> InvokeAndCaptureAsync(
        Func<Task<ChatResponse>> invoke,
        string? userMessage)
    {
        var response = await invoke().ConfigureAwait(false);
        collector.Record(new ChatTranscriptRecord(
            LlmInvoked: true,
            UserMessageSentToModel: userMessage,
            RawModelResponseText: response.Text,
            CapturedAtUtc: DateTimeOffset.UtcNow));
        return response;
    }
}
