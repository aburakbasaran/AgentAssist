using AgentAssist.Application.Abstractions;

namespace AgentAssist.Infrastructure.Identity;

/// <summary>
/// Fixed pilot user context used when the header-based provider is not registered (Mock mode, environments other than <c>Development</c>/<c>InternalPilot</c>, or unit tests). Returns a deterministic <c>pilot-user</c> with <c>agent</c> role and <c>branch-a</c> location.
/// </summary>
public sealed class MockUserContextProvider : IUserContextProvider
{
    /// <inheritdoc />
    public string? UserId => "pilot-user";

    /// <inheritdoc />
    public IReadOnlyList<string> Roles { get; } = ["agent"];

    /// <inheritdoc />
    public string? Location => "branch-a";

    /// <inheritdoc />
    public bool IsAuthenticated => false;
}
