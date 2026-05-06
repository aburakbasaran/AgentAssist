namespace AgentAssist.Application.Feedback;

/// <summary>
/// Represents a piece of pilot feedback referring to an assistant answer.
/// </summary>
public sealed record FeedbackRecord
{
    /// <summary>
    /// The correlation identifier of the assistant query the feedback refers to.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// The user identifier when one is available.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Whether the user reported the answer as helpful.
    /// </summary>
    public required bool Helpful { get; init; }

    /// <summary>
    /// Optional free-form reason text supplied by the user.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// The timestamp from the injected <see cref="TimeProvider"/>.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
}
