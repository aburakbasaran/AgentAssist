using AgentAssist.Domain.Exceptions;

namespace AgentAssist.Domain;

/// <summary>
/// Represents a grounded assistant answer or a structured refusal. A non-refused answer must always carry at least one citation.
/// </summary>
public sealed record AssistantAnswer
{
    /// <summary>
    /// Gets the answer text returned to the caller.
    /// </summary>
    public required string AnswerText { get; init; }

    /// <summary>
    /// Gets the citations used to ground the answer.
    /// </summary>
    public required IReadOnlyList<Citation> Citations { get; init; }

    /// <summary>
    /// Gets the answer confidence level.
    /// </summary>
    public required ConfidenceLevel ConfidenceLevel { get; init; }

    /// <summary>
    /// Gets the risk class associated with the answer.
    /// </summary>
    public required RiskClass RiskClass { get; init; }

    /// <summary>
    /// Gets a value indicating whether the answer should be escalated to a human operator.
    /// </summary>
    public required bool EscalationRequired { get; init; }

    /// <summary>
    /// Gets a value indicating whether this response is a structured refusal.
    /// </summary>
    public required bool Refused { get; init; }

    /// <summary>
    /// Gets the refusal reason when the answer is refused.
    /// </summary>
    public string? RefusalReason { get; init; }

    /// <summary>
    /// Creates a grounded assistant answer from at least one supporting citation.
    /// </summary>
    /// <param name="answerText">The grounded answer text.</param>
    /// <param name="citations">The supporting citations; must contain at least one citation.</param>
    /// <param name="confidenceLevel">The answer confidence level.</param>
    /// <param name="riskAssessment">The risk assessment for the original query.</param>
    /// <returns>A grounded, citation-bearing answer.</returns>
    /// <exception cref="UngroundedAnswerException">Thrown when no citations are supplied.</exception>
    public static AssistantAnswer Grounded(
        string answerText,
        IReadOnlyList<Citation> citations,
        ConfidenceLevel confidenceLevel,
        RiskAssessment riskAssessment)
    {
        ArgumentNullException.ThrowIfNull(citations);
        if (citations.Count is 0)
        {
            throw new UngroundedAnswerException();
        }

        return new AssistantAnswer
        {
            AnswerText = answerText,
            Citations = citations,
            ConfidenceLevel = confidenceLevel,
            RiskClass = riskAssessment.RiskClass,
            EscalationRequired = riskAssessment.EscalationRequired,
            Refused = false,
            RefusalReason = null
        };
    }

    /// <summary>
    /// Creates a structured refusal answer.
    /// </summary>
    /// <param name="reason">The refusal reason.</param>
    /// <param name="riskAssessment">The risk assessment for the original query.</param>
    /// <returns>A non-null refused answer.</returns>
    public static AssistantAnswer RefusedAnswer(string reason, RiskAssessment riskAssessment) => new()
    {
        AnswerText = reason,
        Citations = [],
        ConfidenceLevel = ConfidenceLevel.Low,
        RiskClass = riskAssessment.RiskClass,
        EscalationRequired = riskAssessment.EscalationRequired,
        Refused = true,
        RefusalReason = reason
    };

    /// <summary>
    /// Validates the citation invariant: a non-refused answer must carry at least one citation.
    /// </summary>
    /// <exception cref="UngroundedAnswerException">Thrown when the invariant is violated.</exception>
    public void EnsureCitationInvariant()
    {
        if (!Refused && Citations.Count is 0)
        {
            throw new UngroundedAnswerException();
        }
    }
}
