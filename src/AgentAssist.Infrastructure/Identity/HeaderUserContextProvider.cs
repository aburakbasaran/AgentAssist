using System.Text.RegularExpressions;

using AgentAssist.Application.Abstractions;
using AgentAssist.Infrastructure.Azure.Search;

using Microsoft.AspNetCore.Http;

namespace AgentAssist.Infrastructure.Identity;

/// <summary>
/// Pilot user context provider that reads identity from custom <c>X-Agent-*</c> request headers. Header values are validated against length and a conservative character allow-list before reaching the application layer. This provider is registered only when the host environment is <c>Development</c> or <c>InternalPilot</c> and <c>AgentAssistOptions.AllowHeaderUserContext</c> is <see langword="true"/> (see ADR-0010).
/// </summary>
public sealed partial class HeaderUserContextProvider(IHttpContextAccessor httpContextAccessor) : IUserContextProvider
{
    /// <summary>The header carrying the pilot user identifier.</summary>
    public const string UserHeader = "X-Agent-User";

    /// <summary>The header carrying a comma-separated list of pilot user roles.</summary>
    public const string RolesHeader = "X-Agent-Roles";

    /// <summary>The header carrying the pilot user location.</summary>
    public const string LocationHeader = "X-Agent-Location";

    private const int MaxHeaderLength = 200;

    private static readonly string[] AnonymousRoles = ["anon"];

    public string? UserId => SanitizeIdentifier(GetHeader(UserHeader));

    public IReadOnlyList<string> Roles
    {
        get
        {
            var raw = GetHeader(RolesHeader);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return AnonymousRoles;
            }

            var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var sanitized = new List<string>(parts.Length);
            foreach (var part in parts)
            {
                var clean = SanitizeIdentifier(part);
                if (clean is null)
                {
                    continue;
                }

                if (!AzureSearchAllowList.Roles.TryGetValue(clean, out var canonical))
                {
                    continue;
                }

                if (!sanitized.Contains(canonical, StringComparer.Ordinal))
                {
                    sanitized.Add(canonical);
                }
            }

            return sanitized.Count > 0 ? sanitized : AnonymousRoles;
        }
    }

    public string? Location => SanitizeIdentifier(GetHeader(LocationHeader));

    public bool IsAuthenticated => false;

    private string? GetHeader(string name)
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null)
        {
            return null;
        }

        if (!context.Request.Headers.TryGetValue(name, out var values))
        {
            return null;
        }

        var value = values.FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? SanitizeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxHeaderLength)
        {
            return null;
        }

        return AllowedCharacters().IsMatch(trimmed) ? trimmed : null;
    }

    [GeneratedRegex(@"^[a-zA-Z0-9_\-\.]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedCharacters();
}
