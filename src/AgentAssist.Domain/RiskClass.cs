namespace AgentAssist.Domain;

/// <summary>
/// Represents the business risk level associated with a query, source chunk, or generated answer.
/// </summary>
public enum RiskClass
{
    /// <summary>
    /// Indicates that no elevated business risk was detected.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Indicates that the content may require additional care or operator awareness.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// Indicates that the response should be escalated or handled with heightened caution.
    /// </summary>
    High = 2
}
