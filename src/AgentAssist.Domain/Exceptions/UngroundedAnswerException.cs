namespace AgentAssist.Domain.Exceptions;

/// <summary>
/// Represents a non-refused assistant answer that does not carry at least one citation.
/// </summary>
public sealed class UngroundedAnswerException : DomainException
{
    private const string DefaultMessage = "A non-refused assistant answer must include at least one citation.";

    /// <summary>
    /// Initializes a new instance of the <see cref="UngroundedAnswerException"/> class.
    /// </summary>
    public UngroundedAnswerException()
        : base(DefaultMessage)
    {
    }
}
