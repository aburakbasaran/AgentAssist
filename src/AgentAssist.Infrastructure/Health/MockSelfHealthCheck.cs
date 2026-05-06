using AgentAssist.Application.Abstractions;
using AgentAssist.Application.Ai;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AgentAssist.Infrastructure.Health;

/// <summary>
/// Lightweight readiness probe for Mock-mode deployments. Confirms the core in-memory composition is wired (knowledge service, prompt provider, audit sink, feedback sink) without hitting any external dependency. Intentionally returns <see cref="HealthStatus.Healthy"/> only when all four singletons are resolvable; otherwise <see cref="HealthStatus.Unhealthy"/>.
/// </summary>
internal sealed class MockSelfHealthCheck(
    IKnowledgeSearchService knowledgeSearch,
    IPromptProvider promptProvider,
    IAuditEventSink auditSink,
    IFeedbackSink feedbackSink) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (knowledgeSearch is null || promptProvider is null || auditSink is null || feedbackSink is null)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("One or more Mock-mode singletons could not be resolved."));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Mock-mode self check OK."));
    }
}
