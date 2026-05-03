namespace AgentAssist.Domain.Exceptions;

/// <summary>
/// Represents the base exception type for domain-level failures.
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class.
    /// </summary>
    /// <param name="message">The domain failure message.</param>
    protected DomainException(string message)
        : base(message)
    {
    }
}
