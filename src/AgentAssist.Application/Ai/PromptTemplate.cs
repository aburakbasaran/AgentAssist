namespace AgentAssist.Application.Ai;

/// <summary>
/// Represents a prompt template with system and user message sections.
/// </summary>
/// <param name="TemplateId">The stable template identifier.</param>
/// <param name="SystemMessage">The system message text.</param>
/// <param name="UserMessageFormat">The user message format text.</param>
public sealed record PromptTemplate(string TemplateId, string SystemMessage, string UserMessageFormat);
