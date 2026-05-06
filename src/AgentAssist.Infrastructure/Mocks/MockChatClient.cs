using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Extensions.AI;

namespace AgentAssist.Infrastructure.Mocks;

internal sealed partial class MockChatClient : IChatClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(messages);

        var userMessage = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? string.Empty;
        var json = BuildEnvelopeJson(userMessage);
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, json));

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

    private static string BuildEnvelopeJson(string userMessage)
    {
        var chunkIds = ExtractChunkIds(userMessage);
        if (chunkIds.Count is 0)
        {
            var refused = new
            {
                answerText = "Kayıtlı kaynaklardan yanıt üretilemedi.",
                citations = Array.Empty<string>(),
                confidence = "Low",
                refused = true,
                refusalReason = "no_source"
            };
            return JsonSerializer.Serialize(refused, JsonOptions);
        }

        var grounded = new
        {
            answerText = $"Kaynaklara göre yanıt: {chunkIds[0]}",
            citations = new[] { chunkIds[0] },
            confidence = "High",
            refused = false,
            refusalReason = (string?)null
        };
        return JsonSerializer.Serialize(grounded, JsonOptions);
    }

    private static IReadOnlyList<string> ExtractChunkIds(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return [];
        }

        var matches = ChunkIdPattern().Matches(userMessage);
        var ids = new List<string>(matches.Count);
        foreach (Match match in matches)
        {
            if (match.Groups[1].Success)
            {
                ids.Add(match.Groups[1].Value);
            }
        }

        return ids;
    }

    [GeneratedRegex("chunkId=\"([^\"\n]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex ChunkIdPattern();
}
