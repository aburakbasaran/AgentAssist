using AgentAssist.Application.Configuration;
using AgentAssist.Domain;

namespace AgentAssist.Application.Auditing;

/// <summary>
/// Represents an auditable assistant workflow outcome. The raw question is intentionally not stored; <see cref="QuestionHash"/> and <see cref="QuestionPreview"/> provide deterministic linkage and a sanitized preview without retaining PII.
/// </summary>
public sealed record AuditEvent
{
    /// <summary>
    /// The timestamp from the injected <see cref="TimeProvider"/>.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// The correlation identifier propagated from the request.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// The runtime mode that produced the answer.
    /// </summary>
    public required AgentAssistMode Mode { get; init; }

    /// <summary>
    /// The user identifier when one is available.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// SHA-256 hex hash of the original question. Stable across identical questions; never reversible.
    /// </summary>
    public required string QuestionHash { get; init; }

    /// <summary>
    /// A sanitized truncated preview of the question (max 80 characters, sensitive number patterns redacted).
    /// </summary>
    public required string QuestionPreview { get; init; }

    /// <summary>
    /// The number of retrieved chunks that fed the model.
    /// </summary>
    public required int RetrievalCount { get; init; }

    /// <summary>
    /// The number of citations in the answer.
    /// </summary>
    public required int CitationCount { get; init; }

    /// <summary>
    /// The answer confidence level.
    /// </summary>
    public required ConfidenceLevel ConfidenceLevel { get; init; }

    /// <summary>
    /// The risk class for the workflow.
    /// </summary>
    public required RiskClass RiskClass { get; init; }

    /// <summary>
    /// Whether the answer should be escalated to a human operator.
    /// </summary>
    public required bool EscalationRequired { get; init; }

    /// <summary>
    /// Whether the answer was refused.
    /// </summary>
    public required bool Refused { get; init; }

    /// <summary>
    /// The refusal reason when <see cref="Refused"/> is <see langword="true"/>.
    /// </summary>
    public string? RefusalReason { get; init; }

    /// <summary>
    /// End-to-end latency in milliseconds for the request.
    /// </summary>
    public required long LatencyMs { get; init; }
}
