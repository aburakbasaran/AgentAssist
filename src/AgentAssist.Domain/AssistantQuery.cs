namespace AgentAssist.Domain;

/// <summary>
/// Represents an end-user question submitted to the assistant.
/// </summary>
public sealed record AssistantQuery
{
    /// <summary>
    /// Gets the question text.
    /// </summary>
    public required string Question { get; init; }

    /// <summary>
    /// Gets the user identifier when one is available.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Gets the caller roles used for knowledge filtering.
    /// </summary>
    public required IReadOnlyList<string> Roles { get; init; }
}
