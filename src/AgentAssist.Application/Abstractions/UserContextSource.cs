namespace AgentAssist.Application.Abstractions;

/// <summary>
/// Identifies which <see cref="IUserContextProvider"/> implementation should be activated by the composition root.
/// </summary>
public enum UserContextSource
{
    /// <summary>
    /// No header context, no test fixture; production-like default. The composition root must register an authentication-backed provider or fall back to the anonymous provider.
    /// </summary>
    None = 0,

    /// <summary>
    /// Deterministic in-memory mock provider. Used by Mock-mode runs and unit tests; never used in production environments.
    /// </summary>
    Mock = 1,

    /// <summary>
    /// Header-based pilot provider that reads <c>X-Agent-*</c> request headers. Permitted only in <c>Development</c> or <c>InternalPilot</c> environments with <c>AgentAssistOptions.AllowHeaderUserContext</c> = <see langword="true"/>; explicitly blocked in <c>Production</c> (see ADR-0010).
    /// </summary>
    Header = 2,
}
