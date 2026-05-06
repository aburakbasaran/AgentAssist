using AgentAssist.Application.Abstractions;
using AgentAssist.Application.Feedback;
using AgentAssist.Infrastructure.Persistence.Mappers;

namespace AgentAssist.Infrastructure.Persistence;

internal sealed class SqlFeedbackSink(AgentAssistDbContext dbContext) : IFeedbackSink
{
    public async ValueTask WriteAsync(FeedbackRecord record, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);

        var entity = FeedbackRecordMapper.ToEntity(record);
        dbContext.FeedbackRecords.Add(entity);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
