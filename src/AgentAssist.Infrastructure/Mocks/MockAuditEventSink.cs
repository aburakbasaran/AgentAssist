using AgentAssist.Application.Abstractions;
using AgentAssist.Application.Auditing;
using Microsoft.Extensions.Logging;

namespace AgentAssist.Infrastructure.Mocks;

internal sealed class MockAuditEventSink(ILogger<MockAuditEventSink> logger) : IAuditEventSink
{
    public ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        logger.LogInformation(
            "Assistant audit event accepted. Timestamp={Timestamp} UserId={UserId} RiskClass={RiskClass} Refused={Refused} CitationCount={CitationCount}",
            auditEvent.Timestamp,
            auditEvent.UserId,
            auditEvent.RiskClass,
            auditEvent.Refused,
            auditEvent.CitationCount);

        return ValueTask.CompletedTask;
    }
}
