using AgentAssist.Application.Abstractions;
using AgentAssist.Infrastructure.Identity;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AgentAssist.Infrastructure.DependencyInjection;

/// <summary>
/// DI registration helpers for the pilot <see cref="IUserContextProvider"/>. The composition root (typically <c>Program.cs</c>) decides which <see cref="UserContextSource"/> is permitted, based on the host environment and the <c>AgentAssistOptions.AllowHeaderUserContext</c> flag (see ADR-0010).
/// </summary>
public static class PilotUserContextServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="IUserContextProvider"/> implementation chosen by <paramref name="source"/>:
    /// <list type="bullet">
    /// <item><see cref="UserContextSource.Header"/> &#8594; <see cref="HeaderUserContextProvider"/> (only safe in Development/InternalPilot).</item>
    /// <item><see cref="UserContextSource.Mock"/> &#8594; <see cref="MockUserContextProvider"/> (deterministic agent identity for tests / Mock mode).</item>
    /// <item><see cref="UserContextSource.None"/> &#8594; <see cref="AnonymousUserContextProvider"/> (deny-by-default; production-like deployments without authentication wiring).</item>
    /// </list>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="source">The selected pilot user context source.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPilotUserContext(this IServiceCollection services, UserContextSource source)
    {
        ArgumentNullException.ThrowIfNull(services);

        switch (source)
        {
            case UserContextSource.Header:
                services.AddHttpContextAccessor();
                services.AddScoped<IUserContextProvider, HeaderUserContextProvider>();
                break;
            case UserContextSource.Mock:
                services.AddSingleton<IUserContextProvider, MockUserContextProvider>();
                break;
            case UserContextSource.None:
            default:
                services.AddSingleton<IUserContextProvider, AnonymousUserContextProvider>();
                break;
        }

        return services;
    }
}
