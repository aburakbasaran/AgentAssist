namespace AgentAssist.Api.Contracts;

internal sealed record FeedbackRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public required string CorrelationId { get; init; }

    [Required]
    public required bool Helpful { get; init; }

    [StringLength(2000)]
    public string? Reason { get; init; }
}
