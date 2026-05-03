using AgentAssist.Domain;

namespace AgentAssist.Application.Abstractions;

/// <summary>
/// Classifies the business risk of an assistant query.
/// </summary>
public interface IRiskClassifier
{
    /// <summary>
    /// Classifies a query.
    /// </summary>
    /// <param name="query">The assistant query.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The query risk assessment.</returns>
    ValueTask<RiskAssessment> ClassifyAsync(AssistantQuery query, CancellationToken ct);
}
