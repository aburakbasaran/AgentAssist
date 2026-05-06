using System.Text.Json.Serialization;

namespace AgentAssist.Infrastructure.Azure.Search;

/// <summary>
/// Wire-format DTO for an Azure AI Search index document. Field names use camelCase to match the JSON schema documented in <c>docs/azure/search-index-schema.md</c>.
/// </summary>
public sealed class AzureSearchDocument
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("documentId")]
    public string DocumentId { get; init; } = string.Empty;

    [JsonPropertyName("chunkId")]
    public string ChunkId { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("allowedRoles")]
    public IReadOnlyList<string> AllowedRoles { get; init; } = [];

    [JsonPropertyName("documentType")]
    public string DocumentType { get; init; } = string.Empty;

    [JsonPropertyName("riskLevel")]
    public string RiskLevel { get; init; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }

    [JsonPropertyName("location")]
    public string? Location { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }
}
