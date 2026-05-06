using AgentAssist.Application.Configuration;
using AgentAssist.Domain;

namespace AgentAssist.Application.Abstractions;

/// <summary>
/// Vendor-neutral metric surface used by <see cref="AgentAssist.Application.Assistant.AnswerAssistantQueryHandler"/>. Concrete adapters in the Infrastructure layer publish these signals via <c>System.Diagnostics.Metrics</c> / OpenTelemetry; the default no-op implementation in this package keeps the handler honest in unit tests.
/// </summary>
public interface IAgentAssistMetrics
{
    /// <summary>
    /// Records the end-to-end latency of an assistant query in milliseconds.
    /// </summary>
    void RecordQueryLatency(long latencyMs, AgentAssistMode mode);

    /// <summary>
    /// Records the number of retrieved chunks for a query.
    /// </summary>
    void RecordRetrievalCount(int count, AgentAssistMode mode);

    /// <summary>
    /// Records the number of citations produced for an answer.
    /// </summary>
    void RecordCitationCount(int count, AgentAssistMode mode);

    /// <summary>
    /// Records the confidence level returned by the answer envelope.
    /// </summary>
    void RecordConfidence(ConfidenceLevel confidence, AgentAssistMode mode);

    /// <summary>
    /// Records the classified risk class for the inbound query.
    /// </summary>
    void RecordRiskClass(RiskClass riskClass, AgentAssistMode mode);

    /// <summary>
    /// Increments the refused-answer counter, tagged by refusal reason.
    /// </summary>
    void RecordRefusal(string refusalReason, AgentAssistMode mode);

    /// <summary>
    /// Increments the escalation counter, tagged by classified risk class.
    /// </summary>
    void RecordEscalation(RiskClass riskClass, AgentAssistMode mode);

    /// <summary>
    /// Increments the swallowed audit-write-failed counter (best-effort policy, ADR-0009).
    /// </summary>
    void RecordAuditWriteFailed(AgentAssistMode mode);

    /// <summary>
    /// Increments the per-query provider-mode counter (<c>agentassist.provider_mode</c>) so dashboards can produce a distinct "queries served by Mock vs DevCloud" panel without relying on dimension splits.
    /// </summary>
    void RecordProviderMode(AgentAssistMode mode);

    /// <summary>
    /// Records the input (prompt) and output (completion) token counts reported by the upstream <c>Microsoft.Extensions.AI</c> chat client. Either value may be <see langword="null"/> when the underlying provider does not surface usage details (e.g., the in-memory mock); in that case the corresponding histogram is not updated.
    /// </summary>
    void RecordTokenUsage(long? inputTokens, long? outputTokens, AgentAssistMode mode);
}

/// <summary>
/// Default metric sink that drops every measurement on the floor. Registered when no other adapter is supplied (unit tests, environments without observability wired in).
/// </summary>
public sealed class NullAgentAssistMetrics : IAgentAssistMetrics
{
    /// <inheritdoc />
    public void RecordQueryLatency(long latencyMs, AgentAssistMode mode) { }

    /// <inheritdoc />
    public void RecordRetrievalCount(int count, AgentAssistMode mode) { }

    /// <inheritdoc />
    public void RecordCitationCount(int count, AgentAssistMode mode) { }

    /// <inheritdoc />
    public void RecordConfidence(ConfidenceLevel confidence, AgentAssistMode mode) { }

    /// <inheritdoc />
    public void RecordRiskClass(RiskClass riskClass, AgentAssistMode mode) { }

    /// <inheritdoc />
    public void RecordRefusal(string refusalReason, AgentAssistMode mode) { }

    /// <inheritdoc />
    public void RecordEscalation(RiskClass riskClass, AgentAssistMode mode) { }

    /// <inheritdoc />
    public void RecordAuditWriteFailed(AgentAssistMode mode) { }

    /// <inheritdoc />
    public void RecordProviderMode(AgentAssistMode mode) { }

    /// <inheritdoc />
    public void RecordTokenUsage(long? inputTokens, long? outputTokens, AgentAssistMode mode) { }
}
