namespace AgentAssist.Application.Abstractions;

/// <summary>
/// Provides the caller's identity context (user identifier, roles, optional location) for assistant queries. This abstraction lets the Application layer remain agnostic of how identity flows in (header-based pilot, JWT bearer, etc.).
/// </summary>
public interface IUserContextProvider
{
    /// <summary>
    /// The current user identifier, or <see langword="null"/> when anonymous.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// The roles assigned to the current user. Each role is allow-list-validated by the retrieval layer.
    /// </summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// The optional location associated with the current user.
    /// </summary>
    string? Location { get; }

    /// <summary>
    /// Whether the current user is authenticated. Always <see langword="false"/> in this internal pilot reference architecture; production deployments should swap this provider for an authentication-backed implementation (see ADR-0010).
    /// </summary>
    bool IsAuthenticated { get; }
}
