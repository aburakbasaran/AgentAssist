using AgentAssist.Application.Abstractions;
using AgentAssist.Application.Configuration;
using AgentAssist.Domain;
using Microsoft.Extensions.Options;

namespace AgentAssist.Infrastructure.Mocks;

internal sealed class MockKnowledgeSearchService(IOptions<AgentAssistOptions> options) : IKnowledgeSearchService
{
    private readonly AgentAssistOptions _options = options.Value;

    public ValueTask<IReadOnlyList<RetrievedChunk>> SearchAsync(AssistantQuery query, RiskAssessment risk, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var matches = new List<RetrievedChunk>();
        foreach (var chunk in SampleKnowledge.Chunks)
        {
            if (matches.Count >= _options.MaxRetrievedChunks)
            {
                break;
            }

            if (chunk.Score < _options.MinChunkScore || !HasAllowedRole(query, chunk))
            {
                continue;
            }

            if (MatchesQuery(query.Question, chunk))
            {
                matches.Add(chunk);
            }
        }

        return ValueTask.FromResult<IReadOnlyList<RetrievedChunk>>(matches);
    }

    private static bool HasAllowedRole(AssistantQuery query, RetrievedChunk chunk)
    {
        foreach (var role in query.Roles)
        {
            foreach (var allowedRole in chunk.AllowedRoles)
            {
                if (string.Equals(role, allowedRole, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool MatchesQuery(string question, RetrievedChunk chunk)
    {
        var searchable = string.Concat(chunk.Title, " ", chunk.Content);
        var terms = question.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var term in terms)
        {
            if (term.Length < 3)
            {
                continue;
            }

            if (searchable.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
