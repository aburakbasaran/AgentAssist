using System.Text.Json;

using AgentAssist.Domain;
using AgentAssist.Infrastructure.Azure.Search;
using AgentAssist.Testing;

using Azure.Search.Documents;
using Azure.Search.Documents.Models;

using Microsoft.Extensions.DependencyInjection;

namespace AgentAssist.Evaluation.Tests.DevCloud;

/// <summary>
/// DevCloud-only HR-001/HR-002 retrieval and E2E diagnostics (FAZ 3.5 closure). Writes eval/results/hr-golden-diagnostics.json.
/// </summary>
public sealed class HrGoldenDiagnosticsTests
{
    [Fact]
    public async Task HrGolden_WriteRetrievalAndE2E_Diagnostics()
    {
        if (EvalHostConfiguration.ResolveMode() is not EvalHostMode.DevCloud)
        {
            Assert.Skip("Set EVAL_MODE=DevCloud.");
        }

        var ct = TestContext.Current.CancellationToken;
        var hr001 = LoadGoldenCase("HR-001");
        var hr002 = LoadGoldenCase("HR-002");

        using var factory = new Layer1DevCloudWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var searchClient = scope.ServiceProvider.GetRequiredService<SearchClient>();
        var minChunkScore = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentAssist.Application.Configuration.AgentAssistOptions>>()
            .Value.MinChunkScore;

        var hr001Hits = await SearchTopHitsAsync(searchClient, hr001, ct);
        var hr002Hits = await SearchTopHitsAsync(searchClient, hr002, ct);

        factory.TranscriptCollector.Clear();
        var (hr002Status, hr002Answer) = await PostQueryAsync(factory, hr002, ct);
        var hr002Transcript = factory.TranscriptCollector.Last;

        var payload = new
        {
            runStartedUtc = DateTimeOffset.UtcNow,
            minChunkScore,
            hr001 = new
            {
                caseId = hr001.Id,
                question = hr001.Question,
                expectedChunkId = "CHK-006",
                topHits = hr001Hits,
                chk006PassesMinChunkScore = hr001Hits.FirstOrDefault(h => h.ChunkId == "CHK-006") is { } c && c.NormalizedScore >= minChunkScore
            },
            hr002 = new
            {
                caseId = hr002.Id,
                question = hr002.Question,
                expectedChunkId = "CHK-006",
                topHits = hr002Hits,
                e2e = new
                {
                    httpStatusCode = (int)hr002Status,
                    hr002Answer.Refused,
                    hr002Answer.RefusalReason,
                    hr002Answer.EscalationRequired,
                    citationIds = hr002Answer.Citations.Select(c => c.ChunkId).ToArray(),
                    transcript = hr002Transcript is null
                        ? null
                        : new
                        {
                            hr002Transcript.LlmInvoked,
                            hr002Transcript.UserMessageSentToModel,
                            hr002Transcript.RawModelResponseText
                        }
                }
            }
        };

        WriteResults(payload);
    }

    private static async Task<List<HitRow>> SearchTopHitsAsync(
        SearchClient searchClient,
        GoldenSetCase golden,
        CancellationToken ct)
    {
        var filter = AzureSearchFilterBuilder.Build(golden.Roles, documentType: null, location: "branch-a");
        var options = new SearchOptions
        {
            Filter = filter,
            Size = 8,
            QueryType = SearchQueryType.Semantic,
            SemanticSearch = new SemanticSearchOptions { SemanticConfigurationName = "agentassist-semantic" }
        };

        var response = await searchClient.SearchAsync<AzureSearchDocument>(golden.Question, options, ct);
        var hits = new List<HitRow>();
        await foreach (var hit in response.Value.GetResultsAsync())
        {
            if (hit.Document is null)
            {
                continue;
            }

            var raw = hit.SemanticSearch?.RerankerScore ?? hit.Score ?? 0D;
            hits.Add(new HitRow(hit.Document.ChunkId, AzureSearchDocumentMapper.NormalizeScore(raw)));
        }

        return hits.OrderByDescending(h => h.NormalizedScore).Take(6).ToList();
    }

    private static async Task<(System.Net.HttpStatusCode StatusCode, AssistantAnswer Answer)> PostQueryAsync(
        Layer1DevCloudWebApplicationFactory factory,
        GoldenSetCase golden,
        CancellationToken ct)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Agent-User", "pilot-user");
        client.DefaultRequestHeaders.Add("X-Agent-Roles", string.Join(',', golden.Roles));
        client.DefaultRequestHeaders.Add("X-Agent-Location", "branch-a");

        var response = await client.PostAsJsonAsync("/api/v1/assistant/query", new { question = golden.Question }, ct);
        if (response.StatusCode is not System.Net.HttpStatusCode.OK)
        {
            return (response.StatusCode, AssistantAnswer.RefusedAnswer(
                $"HTTP {(int)response.StatusCode}",
                new RiskAssessment { RiskClass = RiskClass.Low, Reason = "http_error" }));
        }

        var answer = await response.Content.ReadFromJsonAsync<AssistantAnswer>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            ct);
        return (response.StatusCode, answer ?? throw new InvalidOperationException("No answer body."));
    }

    private static GoldenSetCase LoadGoldenCase(string id)
    {
        var path = Path.Combine(AppContext.BaseDirectory, EvaluationHarnessFixture.GoldenSetFile);
        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var item = JsonSerializer.Deserialize<GoldenSetCase>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (item is not null && string.Equals(item.Id, id, StringComparison.Ordinal))
            {
                return item;
            }
        }

        throw new InvalidOperationException($"Case {id} not found.");
    }

    private static void WriteResults(object summary)
    {
        var directory = ResolveResultsDirectory();
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        File.WriteAllText(Path.Combine(directory, "hr-golden-diagnostics.json"), json);
    }

    private static string ResolveResultsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 10 && directory is not null; depth++)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "eval")))
            {
                return Path.Combine(directory.FullName, "eval", "results");
            }

            directory = directory.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "eval", "results");
    }

    private sealed record HitRow(string ChunkId, double NormalizedScore);
}
