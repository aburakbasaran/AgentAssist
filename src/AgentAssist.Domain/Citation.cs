namespace AgentAssist.Domain;

/// <summary>
/// Identifies a source chunk used to ground an assistant answer.
/// </summary>
public sealed record Citation
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
    /// Gets the human-readable source title.
    /// </summary>
    public required string Title { get; init; }
}
