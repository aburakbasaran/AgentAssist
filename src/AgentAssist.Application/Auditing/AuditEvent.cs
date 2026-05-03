using AgentAssist.Domain;

namespace AgentAssist.Application.Auditing;

/// <summary>
/// Represents an auditable assistant workflow outcome.
/// </summary>
public sealed record AuditEvent
{
    /// <summary>
    /// Gets the timestamp from the injected time provider.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the original question.
    /// </summary>
    public required string Question { get; init; }

    /// <summary>
    /// Gets the user identifier when one is available.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Gets the risk class for the workflow.
    /// </summary>
    public required RiskClass RiskClass { get; init; }

    /// <summary>
    /// Gets a value indicating whether the answer was refused.
    /// </summary>
    public required bool Refused { get; init; }

    /// <summary>
    /// Gets the number of citations in the answer.
    /// </summary>
    public required int CitationCount { get; init; }
}
