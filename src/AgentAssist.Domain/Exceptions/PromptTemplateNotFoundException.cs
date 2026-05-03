namespace AgentAssist.Domain.Exceptions;

/// <summary>
/// Represents a failure to locate a required prompt template.
/// </summary>
public sealed class PromptTemplateNotFoundException : DomainException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PromptTemplateNotFoundException"/> class.
    /// </summary>
    /// <param name="templateId">The missing template identifier.</param>
    public PromptTemplateNotFoundException(string templateId)
        : base($"Prompt template '{templateId}' was not found.")
    {
        TemplateId = templateId;
    }

    /// <summary>
    /// Gets the missing template identifier.
    /// </summary>
    public string TemplateId { get; }
}
