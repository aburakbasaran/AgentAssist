using AgentAssist.Application.Abstractions;
using AgentAssist.Application.Auditing;
using Microsoft.Extensions.Logging;

namespace AgentAssist.Infrastructure.Mocks;

internal sealed class MockAuditEventSink(ILogger<MockAuditEventSink> logger) : IAuditEventSink
{
    public ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(auditEvent);

        logger.LogInformation(
            "Assistant audit event accepted. CorrelationId={CorrelationId} Mode={Mode} Timestamp={Timestamp} UserId={UserId} QuestionHash={QuestionHash} Retrieval={RetrievalCount} Citations={CitationCount} Confidence={ConfidenceLevel} Risk={RiskClass} Escalation={EscalationRequired} Refused={Refused} RefusalReason={RefusalReason} LatencyMs={LatencyMs}",
            auditEvent.CorrelationId,
            auditEvent.Mode,
            auditEvent.Timestamp,
            auditEvent.UserId,
            auditEvent.QuestionHash,
            auditEvent.RetrievalCount,
            auditEvent.CitationCount,
            auditEvent.ConfidenceLevel,
            auditEvent.RiskClass,
            auditEvent.EscalationRequired,
            auditEvent.Refused,
            auditEvent.RefusalReason,
            auditEvent.LatencyMs);

        return ValueTask.CompletedTask;
    }
}
