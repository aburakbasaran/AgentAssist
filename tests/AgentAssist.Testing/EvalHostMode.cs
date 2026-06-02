namespace AgentAssist.Testing;

/// <summary>
/// Selects how in-process test hosts configure the API (Mock vs real DevCloud adapters).
/// </summary>
public enum EvalHostMode
{
    /// <summary>
    /// Deterministic mock infrastructure; default when <c>EVAL_MODE</c> is unset (CI).
    /// </summary>
    Mock,

    /// <summary>
    /// Azure-backed adapters; requires credentials and Azure configuration via environment and/or user-secrets.
    /// </summary>
    DevCloud
}
