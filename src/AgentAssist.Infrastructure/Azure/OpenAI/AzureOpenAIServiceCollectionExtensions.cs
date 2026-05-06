using global::Azure.AI.OpenAI;
using global::Azure.Identity;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentAssist.Infrastructure.Azure.OpenAI;

/// <summary>
/// DI registration helpers for the Azure OpenAI / Foundry chat client and embedding generator. Application code keeps consuming <see cref="IChatClient"/> and <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>; only Infrastructure registration changes between mock and DevCloud modes.
/// </summary>
public static class AzureOpenAIServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AzureOpenAIClient"/>, an <see cref="IChatClient"/> backed by Azure OpenAI, and (when an embedding deployment is configured) an <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> for vector retrieval.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAzureOpenAIChatClient(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<AzureOpenAIOptions>()
            .Bind(configuration.GetSection(AzureOpenAIOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;
            var clientOptions = new AzureOpenAIClientOptions
            {
                NetworkTimeout = TimeSpan.FromSeconds(30)
            };
            clientOptions.RetryPolicy = new global::System.ClientModel.Primitives.ClientRetryPolicy(maxRetries: 3);
            return new AzureOpenAIClient(new Uri(options.Endpoint), new DefaultAzureCredential(), clientOptions);
        });

        services.AddChatClient(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;
            var azureClient = sp.GetRequiredService<AzureOpenAIClient>();
            return azureClient
                .GetChatClient(options.ChatDeploymentName)
                .AsIChatClient();
        });

        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.EmbeddingDeploymentName))
            {
                return new NullEmbeddingGenerator();
            }

            var azureClient = sp.GetRequiredService<AzureOpenAIClient>();
            return azureClient
                .GetEmbeddingClient(options.EmbeddingDeploymentName)
                .AsIEmbeddingGenerator();
        });

        return services;
    }

    private sealed class NullEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public EmbeddingGeneratorMetadata Metadata { get; } = new(nameof(NullEmbeddingGenerator));

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(values);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>());
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            return serviceType.IsInstanceOfType(this) ? this : null;
        }

        public void Dispose()
        {
        }
    }
}
