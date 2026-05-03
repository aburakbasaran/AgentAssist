namespace AgentAssist.Domain;

/// <summary>
/// Represents the category of a knowledge document used for grounding an answer.
/// </summary>
public enum DocumentType
{
    /// <summary>
    /// Operational procedure content.
    /// </summary>
    Procedure = 0,

    /// <summary>
    /// Campaign or coverage information.
    /// </summary>
    Campaign = 1,

    /// <summary>
    /// Laboratory or clinical preparation guidance.
    /// </summary>
    Guidance = 2,

    /// <summary>
    /// Administrative process information.
    /// </summary>
    Administrative = 3
}
