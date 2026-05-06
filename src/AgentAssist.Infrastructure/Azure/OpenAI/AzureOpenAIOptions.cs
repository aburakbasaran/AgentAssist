using System.ComponentModel.DataAnnotations;

namespace AgentAssist.Infrastructure.Azure.OpenAI;

/// <summary>
/// Strongly typed configuration for the Azure OpenAI / Foundry adapter.
/// </summary>
public sealed class AzureOpenAIOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "AzureOpenAI";

    /// <summary>
    /// The Azure OpenAI resource endpoint, e.g. <c>https://&lt;name&gt;.openai.azure.com</c>.
    /// </summary>
    [Required]
    [Url]
    public required string Endpoint { get; init; }

    /// <summary>
    /// The chat deployment name.
    /// </summary>
    [Required]
    public required string ChatDeploymentName { get; init; }

    /// <summary>
    /// The embedding deployment name. Optional: when absent, vector retrieval is disabled and the adapter falls back to keyword + semantic ranking.
    /// </summary>
    public string? EmbeddingDeploymentName { get; init; }

    /// <summary>
    /// The maximum number of output tokens the model may produce.
    /// </summary>
    [Range(64, 8000)]
    public int MaxOutputTokens { get; init; } = 800;

    /// <summary>
    /// The chat sampling temperature. Defaults to <c>0</c> for deterministic, cache-friendly grounding.
    /// </summary>
    [Range(0.0D, 2.0D)]
    public double Temperature { get; init; }

    /// <summary>
    /// TTL in seconds for the OpenAI health check cache; <c>0</c> disables caching.
    /// </summary>
    [Range(0, 600)]
    public int HealthCacheSeconds { get; init; } = 60;
}
