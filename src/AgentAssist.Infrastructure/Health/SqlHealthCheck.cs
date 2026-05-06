using AgentAssist.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AgentAssist.Infrastructure.Health;

/// <summary>
/// Lightweight reachability check for the audit/feedback database via <see cref="DbContext.Database"/>.<see cref="DatabaseFacade.CanConnectAsync(CancellationToken)"/>.
/// </summary>
internal sealed class SqlHealthCheck(AgentAssistDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
            return canConnect
                ? HealthCheckResult.Healthy("Audit database reachable.")
                : HealthCheckResult.Unhealthy("Audit database is not reachable.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Audit database probe failed.", ex);
        }
    }
}
