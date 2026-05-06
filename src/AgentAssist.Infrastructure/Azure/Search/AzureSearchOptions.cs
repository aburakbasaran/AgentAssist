using System.ComponentModel.DataAnnotations;

namespace AgentAssist.Infrastructure.Azure.Search;

/// <summary>
/// Strongly typed configuration for the Azure AI Search adapter.
/// </summary>
public sealed class AzureSearchOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "AzureSearch";

    /// <summary>
    /// The Azure AI Search service endpoint, e.g. <c>https://&lt;name&gt;.search.windows.net</c>.
    /// </summary>
    [Required]
    [Url]
    public required string Endpoint { get; init; }

    /// <summary>
    /// The index name that holds the assistant knowledge documents.
    /// </summary>
    [Required]
    public required string IndexName { get; init; }

    /// <summary>
    /// The semantic ranker configuration name attached to the index. Optional; when absent semantic ranking is disabled.
    /// </summary>
    public string? SemanticConfigurationName { get; init; }

    /// <summary>
    /// The vector field name used for hybrid retrieval. Optional; when absent vector search is disabled.
    /// </summary>
    public string? VectorFieldName { get; init; }

    /// <summary>
    /// The number of nearest neighbours requested from vector search.
    /// </summary>
    [Range(1, 100)]
    public int VectorKNearest { get; init; } = 10;
}
