using AgentAssist.Application.Abstractions;
using AgentAssist.Application.Ai;
using AgentAssist.Infrastructure.Ai;
using AgentAssist.Infrastructure.Health;
using AgentAssist.Infrastructure.Mocks;
using AgentAssist.Infrastructure.Observability;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AgentAssist.Infrastructure.DependencyInjection;

/// <summary>
/// Provides dependency injection registration for Phase A mock infrastructure.
/// </summary>
public static class MockInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Adds deterministic mock infrastructure services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMockInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IKnowledgeSearchService, MockKnowledgeSearchService>();
        services.AddSingleton<IRiskClassifier, MockRiskClassifier>();
        services.AddSingleton<IAuditEventSink, MockAuditEventSink>();
        services.AddSingleton<IFeedbackSink, InMemoryFeedbackSink>();
        services.AddSingleton<IPromptProvider, EmbeddedResourcePromptProvider>();
        services.AddChatClient(new MockChatClient());

        // reason: keep the same observability surface area in Mock mode so handler metric emissions don't NRE; the meter is exported only when an Application Insights connection string is configured (no-op in Mock by default).
        services.AddSingleton<AgentAssistMeter>();
        services.AddSingleton<IAgentAssistMetrics, AgentAssistMetricsAdapter>();

        services.AddHealthChecks()
            .AddCheck<MockSelfHealthCheck>("mock-self", failureStatus: HealthStatus.Degraded, tags: ["ready", "mock"]);

        return services;
    }
}
