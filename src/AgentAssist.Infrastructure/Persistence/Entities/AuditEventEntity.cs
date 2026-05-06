namespace AgentAssist.Infrastructure.Persistence.Entities;

internal sealed class AuditEventEntity
{
    public long Id { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public string Mode { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public string QuestionHash { get; set; } = string.Empty;

    public string QuestionPreview { get; set; } = string.Empty;

    public int RetrievalCount { get; set; }

    public int CitationCount { get; set; }

    public string ConfidenceLevel { get; set; } = string.Empty;

    public string RiskClass { get; set; } = string.Empty;

    public bool EscalationRequired { get; set; }

    public bool Refused { get; set; }

    public string? RefusalReason { get; set; }

    public long LatencyMs { get; set; }
}
