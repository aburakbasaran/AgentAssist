using AgentAssist.Application.Abstractions;
using AgentAssist.Application.Feedback;

using Microsoft.Extensions.Logging;

namespace AgentAssist.Infrastructure.Mocks;

internal sealed class InMemoryFeedbackSink(ILogger<InMemoryFeedbackSink> logger) : IFeedbackSink
{
    public ValueTask WriteAsync(FeedbackRecord record, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(record);

        logger.LogInformation(
            "Pilot feedback accepted. CorrelationId={CorrelationId} Helpful={Helpful} Reason={Reason} Timestamp={Timestamp}",
            record.CorrelationId,
            record.Helpful,
            record.Reason,
            record.Timestamp);

        return ValueTask.CompletedTask;
    }
}
