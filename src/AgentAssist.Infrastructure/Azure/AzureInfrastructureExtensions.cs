using Microsoft.Extensions.DependencyInjection;

namespace AgentAssist.Infrastructure.Azure;

/// <summary>
/// Provides dependency injection registration for cloud infrastructure adapters.
/// </summary>
public static class AzureInfrastructureExtensions
{
    /// <summary>
    /// Adds cloud infrastructure services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAzureInfrastructure(this IServiceCollection services)
    {
        throw new NotImplementedException("Phase A only supports mock infrastructure.");
    }
}
