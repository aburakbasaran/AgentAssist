namespace AgentAssist.Infrastructure.Persistence.Entities;

internal sealed class FeedbackRecordEntity
{
    public long Id { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public bool Helpful { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}
