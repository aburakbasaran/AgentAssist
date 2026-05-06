using System.Text.Json.Serialization;

namespace AgentAssist.Evaluation.Tests;

public sealed record GoldenSetCase
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("category")]
    public required string Category { get; init; }

    [JsonPropertyName("roles")]
    public required IReadOnlyList<string> Roles { get; init; }

    [JsonPropertyName("question")]
    public required string Question { get; init; }

    [JsonPropertyName("expectedRefused")]
    public required bool ExpectedRefused { get; init; }

    [JsonPropertyName("expectedEscalation")]
    public required bool ExpectedEscalation { get; init; }

    [JsonPropertyName("expectedCitationMin")]
    public required int ExpectedCitationMin { get; init; }

    [JsonPropertyName("expectedRoleRestrictedChunkId")]
    public string? ExpectedRoleRestrictedChunkId { get; init; }

    [JsonPropertyName("expectedNoSystemPromptLeak")]
    public bool ExpectedNoSystemPromptLeak { get; init; }
}
