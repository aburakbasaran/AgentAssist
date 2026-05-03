namespace AgentAssist.Application.Ai;

/// <summary>
/// Provides prompt templates by stable template identifier.
/// </summary>
public interface IPromptProvider
{
    /// <summary>
    /// Gets a prompt template.
    /// </summary>
    /// <param name="templateId">The stable template identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The resolved prompt template.</returns>
    ValueTask<PromptTemplate> GetAsync(string templateId, CancellationToken ct);
}
