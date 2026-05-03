namespace AgentAssist.Domain;

/// <summary>
/// Represents a knowledge chunk retrieved for grounding an assistant answer.
/// </summary>
public sealed record RetrievedChunk
{
    /// <summary>
    /// Gets the source document identifier.
    /// </summary>
    public required string DocumentId { get; init; }

    /// <summary>
    /// Gets the source chunk identifier.
    /// </summary>
    public required string ChunkId { get; init; }

    /// <summary>
    /// Gets the source title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the source content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the roles allowed to use this chunk.
    /// </summary>
    public required IReadOnlyList<string> AllowedRoles { get; init; }

    /// <summary>
    /// Gets the document type.
    /// </summary>
    public required DocumentType DocumentType { get; init; }

    /// <summary>
    /// Gets the risk level associated with the chunk.
    /// </summary>
    public required RiskClass RiskLevel { get; init; }

    /// <summary>
    /// Gets the deterministic relevance score assigned by the search service.
    /// </summary>
    public required double Score { get; init; }

    /// <summary>
    /// Converts the retrieved chunk into an answer citation.
    /// </summary>
    /// <returns>A citation pointing at this chunk.</returns>
    public Citation ToCitation() => new()
    {
        DocumentId = DocumentId,
        ChunkId = ChunkId,
        Title = Title
    };
}
