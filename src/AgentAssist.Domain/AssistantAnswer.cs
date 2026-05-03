namespace AgentAssist.Domain;

/// <summary>
/// Represents a grounded assistant answer or a structured refusal.
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
}
