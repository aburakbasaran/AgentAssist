using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

namespace AgentAssist.Infrastructure.Mocks;

internal sealed class MockChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var userMessage = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? string.Empty;
        var answer = BuildAnswer(userMessage);
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, answer));

        return Task.FromResult(response);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        return serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
    }

    private static string BuildAnswer(string userMessage)
    {
        var firstSourceLine = userMessage
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(line => line.StartsWith("[1]", StringComparison.Ordinal));

        return string.IsNullOrWhiteSpace(firstSourceLine)
            ? "Kayıtlı kaynaklardan yanıt üretilemedi."
            : string.Concat("Kaynaklara göre yanıt: ", firstSourceLine);
    }
}
