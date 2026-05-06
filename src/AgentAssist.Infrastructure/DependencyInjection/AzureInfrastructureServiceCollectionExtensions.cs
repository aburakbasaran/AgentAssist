using AgentAssist.Application.Abstractions;
using AgentAssist.Application.Ai;
using AgentAssist.Infrastructure.Ai;
using AgentAssist.Infrastructure.Azure.OpenAI;
using AgentAssist.Infrastructure.Azure.Search;
using AgentAssist.Infrastructure.Health;
using AgentAssist.Infrastructure.Mocks;
using AgentAssist.Infrastructure.Observability;
using AgentAssist.Infrastructure.Persistence;

using global::Azure.Identity;
using global::Azure.Search.Documents;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AgentAssist.Infrastructure.DependencyInjection;

/// <summary>
/// Provides dependency injection registration for cloud-backed infrastructure adapters used by the production pilot's DevCloud mode.
/// </summary>
public static class AzureInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Adds the DevCloud infrastructure composition: Azure AI Search retrieval, Azure OpenAI <see cref="IChatClient"/>, and the embedding generator. Slice 4 swaps the mock audit sink with the SQL one; Slice 6 swaps the user context provider with the header-based pilot provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDevCloudInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<AzureSearchOptions>()
            .Bind(configuration.GetSection(AzureSearchOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureSearchOptions>>().Value;
            var clientOptions = new SearchClientOptions
            {
                Retry =
                {
                    MaxRetries = 3,
                    Mode = global::Azure.Core.RetryMode.Exponential,
                    Delay = TimeSpan.FromMilliseconds(500),
                    MaxDelay = TimeSpan.FromSeconds(5),
                    NetworkTimeout = TimeSpan.FromSeconds(15)
                }
            };
            return new SearchClient(
                new Uri(options.Endpoint),
                options.IndexName,
                new DefaultAzureCredential(),
                clientOptions);
        });

        services.AddSingleton<IKnowledgeSearchService, AzureSearchKnowledgeService>();
        services.AddAzureOpenAIChatClient(configuration);

        var sqlConnectionString = configuration["AzureSql:ConnectionString"];
        services.AddDbContextPool<AgentAssistDbContext>(options =>
        {
            options.UseSqlServer(string.IsNullOrWhiteSpace(sqlConnectionString)
                ? "Server=(localdb)\\mssqllocaldb;Database=AgentAssistAudit;Integrated Security=true"
                : sqlConnectionString);
        });

        services.AddScoped<IAuditEventSink, SqlAuditEventSink>();
        services.AddScoped<IFeedbackSink, SqlFeedbackSink>();

        // reason: risk classifier and prompt provider remain reusable across modes; Slice 6 swaps the user context provider.
        services.AddSingleton<IRiskClassifier, MockRiskClassifier>();
        services.AddSingleton<IPromptProvider, EmbeddedResourcePromptProvider>();

        services.AddMemoryCache();
        services.AddAgentAssistObservability(configuration);

        services.AddHealthChecks()
            .AddCheck<AzureSearchHealthCheck>("azure-search", failureStatus: HealthStatus.Unhealthy, tags: ["ready", "azure", "search"])
            .AddCheck<AzureOpenAIHealthCheck>("azure-openai", failureStatus: HealthStatus.Unhealthy, tags: ["ready", "azure", "openai"])
            .AddCheck<SqlHealthCheck>("audit-sql", failureStatus: HealthStatus.Unhealthy, tags: ["ready", "azure", "sql"]);

        return services;
    }
}
