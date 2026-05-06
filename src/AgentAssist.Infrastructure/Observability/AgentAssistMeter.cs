using System.Diagnostics.Metrics;

namespace AgentAssist.Infrastructure.Observability;

/// <summary>
/// Custom metric source for Agent Assist. Exposes counters and histograms used by the handler to record query latency, retrieval/citation counts, confidence, risk class, escalation, refusal reason tags, and provider mode tags.
/// </summary>
public sealed class AgentAssistMeter : IDisposable
{
    /// <summary>
    /// The meter name; OpenTelemetry exporters subscribe to this source.
    /// </summary>
    public const string MeterName = "AgentAssist";

    private readonly Meter _meter;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentAssistMeter"/> class.
    /// </summary>
    public AgentAssistMeter()
    {
        _meter = new Meter(MeterName, "1.0.0");
        QueryLatency = _meter.CreateHistogram<long>("agentassist.query.latency_ms", "ms", "Assistant query end-to-end latency in milliseconds.");
        RetrievalCount = _meter.CreateHistogram<int>("agentassist.retrieval.count", "{chunks}", "Number of retrieved chunks per query.");
        CitationCount = _meter.CreateHistogram<int>("agentassist.citation.count", "{citations}", "Number of citations included in the answer.");
        Confidence = _meter.CreateCounter<long>("agentassist.confidence", "{answers}", "Number of answers tagged by confidence level (Low/Medium/High).");
        RiskClassCounter = _meter.CreateCounter<long>("agentassist.risk_class", "{queries}", "Number of queries tagged by classified risk class (Low/Medium/High).");
        ProviderModeCounter = _meter.CreateCounter<long>("agentassist.provider_mode", "{queries}", "Number of queries tagged by infrastructure mode (Mock/DevCloud).");
        Refusals = _meter.CreateCounter<long>("agentassist.refusal.total", "{refusals}", "Number of refused answers, tagged by refusal reason.");
        Escalations = _meter.CreateCounter<long>("agentassist.escalation.total", "{escalations}", "Number of answers flagged for escalation, tagged by risk class.");
        AuditWriteFailed = _meter.CreateCounter<long>("agentassist.audit.write_failed", "{failures}", "Number of audit write attempts that failed and were swallowed (best-effort policy, ADR-0009).");
        InputTokens = _meter.CreateHistogram<long>("agentassist.ai.tokens.input", "{tokens}", "Input (prompt) tokens reported by the Microsoft.Extensions.AI ChatResponse.Usage block. Recorded only when the upstream provider returns a usage value.");
        OutputTokens = _meter.CreateHistogram<long>("agentassist.ai.tokens.output", "{tokens}", "Output (completion) tokens reported by the Microsoft.Extensions.AI ChatResponse.Usage block. Recorded only when the upstream provider returns a usage value.");
    }

    /// <summary>
    /// Histogram of end-to-end query latency in milliseconds.
    /// </summary>
    public Histogram<long> QueryLatency { get; }

    /// <summary>
    /// Histogram of retrieved chunk counts per query.
    /// </summary>
    public Histogram<int> RetrievalCount { get; }

    /// <summary>
    /// Histogram of citation counts in answers.
    /// </summary>
    public Histogram<int> CitationCount { get; }

    /// <summary>
    /// Counter of answers tagged by confidence level (Low / Medium / High).
    /// </summary>
    public Counter<long> Confidence { get; }

    /// <summary>
    /// Counter of queries tagged by classified risk class (Low / Medium / High).
    /// </summary>
    public Counter<long> RiskClassCounter { get; }

    /// <summary>
    /// Counter of queries tagged by infrastructure mode (Mock / DevCloud) so dashboards can split metrics per provider.
    /// </summary>
    public Counter<long> ProviderModeCounter { get; }

    /// <summary>
    /// Counter of refused answers, tagged by refusal reason.
    /// </summary>
    public Counter<long> Refusals { get; }

    /// <summary>
    /// Counter of escalation-flagged answers, tagged by risk class.
    /// </summary>
    public Counter<long> Escalations { get; }

    /// <summary>
    /// Counter of audit write failures that were swallowed by the best-effort audit policy (ADR-0009).
    /// </summary>
    public Counter<long> AuditWriteFailed { get; }

    /// <summary>
    /// Histogram of input (prompt) token counts reported by Microsoft.Extensions.AI <c>ChatResponse.Usage.InputTokenCount</c>. Empty distribution when the chat client is in Mock mode (the deterministic mock does not emit usage).
    /// </summary>
    public Histogram<long> InputTokens { get; }

    /// <summary>
    /// Histogram of output (completion) token counts reported by Microsoft.Extensions.AI <c>ChatResponse.Usage.OutputTokenCount</c>. Empty distribution when the chat client is in Mock mode (the deterministic mock does not emit usage).
    /// </summary>
    public Histogram<long> OutputTokens { get; }

    /// <inheritdoc />
    public void Dispose() => _meter.Dispose();
}
