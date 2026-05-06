using AgentAssist.Application.Abstractions;
using AgentAssist.Application.Configuration;
using AgentAssist.Domain;

using global::Azure;
using global::Azure.Search.Documents;
using global::Azure.Search.Documents.Models;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentAssist.Infrastructure.Azure.Search;

/// <summary>
/// Azure AI Search-backed implementation of <see cref="IKnowledgeSearchService"/>. Performs hybrid retrieval (keyword + optional vector + optional semantic ranker) with role/location/documentType filters constructed via the safe <see cref="AzureSearchFilterBuilder"/>.
/// </summary>
internal sealed class AzureSearchKnowledgeService(
    SearchClient searchClient,
    IOptions<AgentAssistOptions> agentOptions,
    IOptions<AzureSearchOptions> searchOptions,
    IEmbeddingGenerator<string, Embedding<float>>? embeddingGenerator,
    ILogger<AzureSearchKnowledgeService> logger)
    : IKnowledgeSearchService
{
    private readonly AgentAssistOptions _agentOptions = agentOptions.Value;
    private readonly AzureSearchOptions _searchOptions = searchOptions.Value;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<RetrievedChunk>> SearchAsync(AssistantQuery query, RiskAssessment risk, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(risk);

        var filter = AzureSearchFilterBuilder.Build(
            query.Roles,
            documentType: null,
            location: query.Location);
        var size = _agentOptions.MaxRetrievedChunks;

        var options = new SearchOptions
        {
            Filter = filter,
            Size = size,
            IncludeTotalCount = false
        };

        if (!string.IsNullOrWhiteSpace(_searchOptions.SemanticConfigurationName))
        {
            options.QueryType = SearchQueryType.Semantic;
            options.SemanticSearch = new SemanticSearchOptions
            {
                SemanticConfigurationName = _searchOptions.SemanticConfigurationName
            };
        }

        if (embeddingGenerator is not null
            && !string.IsNullOrWhiteSpace(_searchOptions.VectorFieldName))
        {
            try
            {
                var embeddings = await embeddingGenerator
                    .GenerateAsync([query.Question], options: null, ct)
                    .ConfigureAwait(false);
                if (embeddings.Count > 0)
                {
                    var vector = embeddings[0].Vector;
                    var vectorQuery = new VectorizedQuery(vector)
                    {
                        KNearestNeighborsCount = _searchOptions.VectorKNearest
                    };
                    vectorQuery.Fields.Add(_searchOptions.VectorFieldName!);
                    options.VectorSearch = new VectorSearchOptions
                    {
                        Queries = { vectorQuery }
                    };
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Embedding generation failed; falling back to keyword search only.");
            }
        }

        Response<SearchResults<AzureSearchDocument>> response;
        try
        {
            response = await searchClient.SearchAsync<AzureSearchDocument>(query.Question, options, ct).ConfigureAwait(false);
        }
        catch (RequestFailedException ex)
        {
            logger.LogWarning(ex, "Azure AI Search request failed for index {IndexName}.", _searchOptions.IndexName);
            return [];
        }

        var matches = new List<RetrievedChunk>(capacity: size);
        await foreach (var hit in response.Value.GetResultsAsync().ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            if (hit.Document is null)
            {
                continue;
            }

            var rawScore = hit.SemanticSearch?.RerankerScore ?? hit.Score ?? 0.0D;
            var chunk = AzureSearchDocumentMapper.ToRetrievedChunk(hit.Document, rawScore);
            if (chunk.Score < _agentOptions.MinChunkScore)
            {
                continue;
            }

            matches.Add(chunk);
            if (matches.Count >= size)
            {
                break;
            }
        }

        return matches;
    }
}
