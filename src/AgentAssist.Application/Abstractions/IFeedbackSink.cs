using AgentAssist.Application.Feedback;

namespace AgentAssist.Application.Abstractions;

/// <summary>
/// Persists pilot feedback records.
/// </summary>
public interface IFeedbackSink
{
    /// <summary>
    /// Writes a feedback record.
    /// </summary>
    /// <param name="record">The feedback record.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the record is accepted.</returns>
    ValueTask WriteAsync(FeedbackRecord record, CancellationToken ct);
}
