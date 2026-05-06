using global::Azure.Monitor.OpenTelemetry.AspNetCore;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace AgentAssist.Infrastructure.Observability;

/// <summary>
/// DI registration helpers for OpenTelemetry traces, metrics, and logs exported to Azure Application Insights via <see cref="AzureMonitorOpenTelemetry"/>. When no <c>ApplicationInsights:ConnectionString</c> is configured the registration is a silent no-op so Mock and unconfigured environments still run.
/// </summary>
public static class AzureMonitorRegistration
{
    /// <summary>
    /// Adds OpenTelemetry + Azure Monitor exporter when <c>ApplicationInsights:ConnectionString</c> is present. Custom metrics from <see cref="AgentAssistMeter"/> are always registered as a singleton.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAgentAssistObservability(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<AgentAssistMeter>();
        services.AddSingleton<AgentAssist.Application.Abstractions.IAgentAssistMetrics, AgentAssistMetricsAdapter>();

        var connectionString = configuration["ApplicationInsights:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.StartsWith('<'))
        {
            return services;
        }

        services.AddOpenTelemetry()
            .UseAzureMonitor(options =>
            {
                options.ConnectionString = connectionString;
            })
            .WithMetrics(metrics => metrics.AddMeter(AgentAssistMeter.MeterName))
            .WithTracing(tracing => tracing.AddSource(AgentAssistMeter.MeterName));

        return services;
    }
}
