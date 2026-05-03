namespace AgentAssist.Domain.Exceptions;

/// <summary>
/// Represents a query that violates assistant domain rules.
/// </summary>
public sealed class InvalidAssistantQueryException : DomainException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidAssistantQueryException"/> class.
    /// </summary>
    /// <param name="message">The validation message.</param>
    public InvalidAssistantQueryException(string message)
        : base(message)
    {
    }
}
