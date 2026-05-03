namespace AgentAssist.Domain;

/// <summary>
/// Represents the risk classification result for a query.
/// </summary>
public sealed record RiskAssessment
{
    /// <summary>
    /// Gets the detected risk class.
    /// </summary>
    public required RiskClass RiskClass { get; init; }

    /// <summary>
    /// Gets the explanation for the classification.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Gets a value indicating whether the answer should be escalated.
    /// </summary>
    public bool EscalationRequired => RiskClass is RiskClass.High;
}
