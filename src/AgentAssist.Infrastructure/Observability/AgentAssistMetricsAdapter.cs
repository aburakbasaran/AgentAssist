using System.Diagnostics.Metrics;

using AgentAssist.Application.Abstractions;
using AgentAssist.Application.Configuration;
using AgentAssist.Domain;

namespace AgentAssist.Infrastructure.Observability;

/// <summary>
/// Adapter that implements the vendor-neutral <see cref="IAgentAssistMetrics"/> contract by writing through to the <see cref="AgentAssistMeter"/> instruments. Registered in both Mock and DevCloud composition roots so the handler always emits metrics; export is gated by the OpenTelemetry / Azure Monitor wiring (no-op when no Application Insights connection string is configured).
/// </summary>
internal sealed class AgentAssistMetricsAdapter(AgentAssistMeter meter) : IAgentAssistMetrics
{
    /// <inheritdoc />
    public void RecordQueryLatency(long latencyMs, AgentAssistMode mode)
    {
        meter.QueryLatency.Record(latencyMs, ModeTag(mode));
    }

    /// <inheritdoc />
    public void RecordRetrievalCount(int count, AgentAssistMode mode)
    {
        meter.RetrievalCount.Record(count, ModeTag(mode));
    }

    /// <inheritdoc />
    public void RecordCitationCount(int count, AgentAssistMode mode)
    {
        meter.CitationCount.Record(count, ModeTag(mode));
    }

    /// <inheritdoc />
    public void RecordConfidence(ConfidenceLevel confidence, AgentAssistMode mode)
    {
        meter.Confidence.Add(1, new KeyValuePair<string, object?>("confidence", confidence.ToString()), ModeTag(mode));
    }

    /// <inheritdoc />
    public void RecordRiskClass(RiskClass riskClass, AgentAssistMode mode)
    {
        meter.RiskClassCounter.Add(1, new KeyValuePair<string, object?>("risk_class", riskClass.ToString()), ModeTag(mode));
    }

    /// <inheritdoc />
    public void RecordRefusal(string refusalReason, AgentAssistMode mode)
    {
        ArgumentNullException.ThrowIfNull(refusalReason);
        meter.Refusals.Add(1, new KeyValuePair<string, object?>("reason", refusalReason), ModeTag(mode));
    }

    /// <inheritdoc />
    public void RecordEscalation(RiskClass riskClass, AgentAssistMode mode)
    {
        meter.Escalations.Add(1, new KeyValuePair<string, object?>("risk_class", riskClass.ToString()), ModeTag(mode));
    }

    /// <inheritdoc />
    public void RecordAuditWriteFailed(AgentAssistMode mode)
    {
        meter.AuditWriteFailed.Add(1, ModeTag(mode));
    }

    /// <inheritdoc />
    public void RecordProviderMode(AgentAssistMode mode)
    {
        meter.ProviderModeCounter.Add(1, ModeTag(mode));
    }

    /// <inheritdoc />
    public void RecordTokenUsage(long? inputTokens, long? outputTokens, AgentAssistMode mode)
    {
        if (inputTokens is { } input and >= 0)
        {
            meter.InputTokens.Record(input, ModeTag(mode));
        }

        if (outputTokens is { } output and >= 0)
        {
            meter.OutputTokens.Record(output, ModeTag(mode));
        }
    }

    private static KeyValuePair<string, object?> ModeTag(AgentAssistMode mode) =>
        new("provider_mode", mode.ToString());
}
