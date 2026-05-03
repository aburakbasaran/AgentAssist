namespace AgentAssist.Domain;

/// <summary>
/// Represents the confidence level of an answer produced from retrieved knowledge.
/// </summary>
public enum ConfidenceLevel
{
    /// <summary>
    /// Indicates that the answer could not be grounded with enough evidence.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Indicates that the answer is grounded but should be reviewed with normal care.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// Indicates that the answer is strongly grounded in the retrieved citations.
    /// </summary>
    High = 2
}
