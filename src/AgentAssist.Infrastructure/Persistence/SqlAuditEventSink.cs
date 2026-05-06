using AgentAssist.Application.Abstractions;
using AgentAssist.Application.Auditing;
using AgentAssist.Infrastructure.Persistence.Mappers;

namespace AgentAssist.Infrastructure.Persistence;

internal sealed class SqlAuditEventSink(AgentAssistDbContext dbContext) : IAuditEventSink
{
    public async ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var entity = AuditEventMapper.ToEntity(auditEvent);
        dbContext.AuditEvents.Add(entity);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
