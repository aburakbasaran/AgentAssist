using AgentAssist.Infrastructure.Azure.OpenAI;

using global::Azure.AI.OpenAI;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AgentAssist.Infrastructure.Health;

/// <summary>
/// Health check for Azure OpenAI / Foundry that intentionally does <b>not</b> issue a chat completion. The probe performs the cheapest verifiable signal: that <see cref="AzureOpenAIClient"/> can be instantiated for the configured endpoint and that a chat deployment name is set. Successful results are cached on <see cref="IMemoryCache"/> with a configurable TTL (default 60 seconds, controlled by <c>AzureOpenAI:HealthCacheSeconds</c>).
/// </summary>
internal sealed class AzureOpenAIHealthCheck(
    AzureOpenAIClient azureOpenAIClient,
    IOptions<AzureOpenAIOptions> options,
    IMemoryCache cache)
    : IHealthCheck
{
    internal const string CacheKey = "AgentAssist:HealthCheck:AzureOpenAI";
    private readonly AzureOpenAIOptions _options = options.Value;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue<HealthCheckResult>(CacheKey, out var cached))
        {
            return Task.FromResult(cached);
        }

        var result = ProbeOnce();

        var ttl = _options.HealthCacheSeconds > 0
            ? TimeSpan.FromSeconds(_options.HealthCacheSeconds)
            : TimeSpan.FromSeconds(60);
        cache.Set(CacheKey, result, ttl);

        return Task.FromResult(result);
    }

    private HealthCheckResult ProbeOnce()
    {
        if (string.IsNullOrWhiteSpace(_options.ChatDeploymentName))
        {
            return HealthCheckResult.Unhealthy("Azure OpenAI chat deployment name is not configured.");
        }

        // reason: intentional minimal probe; an end-to-end "model alive" check would consume tokens and quota and is documented as a future /health/deep follow-up (ADR-0009).
        var endpointConfigured = !string.IsNullOrWhiteSpace(_options.Endpoint);
        return endpointConfigured && azureOpenAIClient is not null
            ? HealthCheckResult.Healthy("Azure OpenAI client constructed; deployment name configured.")
            : HealthCheckResult.Unhealthy("Azure OpenAI endpoint is not configured.");
    }
}
