using global::Azure;
using global::Azure.Search.Documents;
using global::Azure.Search.Documents.Models;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AgentAssist.Infrastructure.Health;

/// <summary>
/// Lightweight reachability check for Azure AI Search. Issues a tiny <c>"*"</c> query with <c>Size=0</c> to verify data-plane reachability without consuming search units. The check has a hard 2 second timeout enforced via cancellation.
/// </summary>
internal sealed class AzureSearchHealthCheck(SearchClient searchClient) : IHealthCheck
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ProbeTimeout);

        try
        {
            var response = await searchClient
                .SearchAsync<SearchDocument>("*", new SearchOptions { Size = 0 }, cts.Token)
                .ConfigureAwait(false);
            return response.HasValue
                ? HealthCheckResult.Healthy("Azure AI Search reachable.")
                : HealthCheckResult.Degraded("Azure AI Search returned an empty response.");
        }
        catch (RequestFailedException ex)
        {
            return HealthCheckResult.Unhealthy($"Azure AI Search request failed (status {ex.Status}).");
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Azure AI Search probe timed out.");
        }
    }
}
