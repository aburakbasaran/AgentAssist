using AgentAssist.Application.Auditing;

namespace AgentAssist.Application.Abstractions;

/// <summary>
/// Writes audit events produced by assistant workflows.
/// </summary>
public interface IAuditEventSink
{
    /// <summary>
    /// Writes an audit event.
    /// </summary>
    /// <param name="auditEvent">The audit event.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the event is accepted.</returns>
    ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken ct);
}
