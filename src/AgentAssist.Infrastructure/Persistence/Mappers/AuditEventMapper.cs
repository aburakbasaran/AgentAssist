using AgentAssist.Application.Auditing;
using AgentAssist.Infrastructure.Persistence.Entities;

namespace AgentAssist.Infrastructure.Persistence.Mappers;

internal static class AuditEventMapper
{
    public static AuditEventEntity ToEntity(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        return new AuditEventEntity
        {
            Timestamp = auditEvent.Timestamp,
            CorrelationId = auditEvent.CorrelationId,
            Mode = auditEvent.Mode.ToString(),
            UserId = auditEvent.UserId,
            QuestionHash = auditEvent.QuestionHash,
            QuestionPreview = auditEvent.QuestionPreview,
            RetrievalCount = auditEvent.RetrievalCount,
            CitationCount = auditEvent.CitationCount,
            ConfidenceLevel = auditEvent.ConfidenceLevel.ToString(),
            RiskClass = auditEvent.RiskClass.ToString(),
            EscalationRequired = auditEvent.EscalationRequired,
            Refused = auditEvent.Refused,
            RefusalReason = auditEvent.RefusalReason,
            LatencyMs = auditEvent.LatencyMs
        };
    }
}
