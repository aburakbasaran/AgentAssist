using AgentAssist.Application.Abstractions;

namespace AgentAssist.Infrastructure.Identity;

/// <summary>
/// Anonymous fallback <see cref="IUserContextProvider"/> registered when neither the header-based pilot provider nor the deterministic mock provider is appropriate (e.g., production-like deployment without an authentication-backed provider yet wired up). Returns <see langword="null"/> identity, the single role <c>anon</c>, and no location, so that retrieval allow-list filters drop the caller to zero results (deny-by-default).
/// </summary>
public sealed class AnonymousUserContextProvider : IUserContextProvider
{
    private static readonly string[] AnonymousRoles = ["anon"];

    /// <inheritdoc />
    public string? UserId => null;

    /// <inheritdoc />
    public IReadOnlyList<string> Roles => AnonymousRoles;

    /// <inheritdoc />
    public string? Location => null;

    /// <inheritdoc />
    public bool IsAuthenticated => false;
}
