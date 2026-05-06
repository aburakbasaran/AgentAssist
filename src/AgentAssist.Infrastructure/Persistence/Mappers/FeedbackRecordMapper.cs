using AgentAssist.Application.Feedback;
using AgentAssist.Infrastructure.Persistence.Entities;

namespace AgentAssist.Infrastructure.Persistence.Mappers;

internal static class FeedbackRecordMapper
{
    public static FeedbackRecordEntity ToEntity(FeedbackRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new FeedbackRecordEntity
        {
            CorrelationId = record.CorrelationId,
            UserId = record.UserId,
            Helpful = record.Helpful,
            Reason = record.Reason,
            Timestamp = record.Timestamp
        };
    }
}
