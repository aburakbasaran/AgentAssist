namespace AgentAssist.Application.Configuration;

/// <summary>
/// Represents the runtime mode for Agent Assist.
/// </summary>
public enum AgentAssistMode
{
    /// <summary>
    /// Uses deterministic in-memory mock services.
    /// </summary>
    Mock = 0,

    /// <summary>
    /// Uses cloud-backed development adapters.
    /// </summary>
    DevCloud = 1
}
