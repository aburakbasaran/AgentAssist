using AgentAssist.Domain;

namespace AgentAssist.Application.Abstractions;

/// <summary>
/// Searches domain knowledge for chunks relevant to an assistant query.
/// </summary>
public interface IKnowledgeSearchService
{
    /// <summary>
    /// Searches for relevant chunks.
    /// </summary>
    /// <param name="query">The assistant query.</param>
    /// <param name="risk">The query risk assessment.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The retrieved chunks.</returns>
    ValueTask<IReadOnlyList<RetrievedChunk>> SearchAsync(AssistantQuery query, RiskAssessment risk, CancellationToken ct);
}
