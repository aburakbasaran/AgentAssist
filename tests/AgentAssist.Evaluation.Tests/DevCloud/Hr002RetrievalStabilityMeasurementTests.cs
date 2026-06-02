using System.Text.Json;

using AgentAssist.Infrastructure.Azure.Search;
using AgentAssist.Testing;

using Azure.Search.Documents;
using Azure.Search.Documents.Models;

using Microsoft.Extensions.DependencyInjection;

namespace AgentAssist.Evaluation.Tests.DevCloud;

/// <summary>
/// Measures semantic reranker score stability for HR-002 query against CHK-006 (FAZ 4 pre-check).
/// Writes eval/results/hr002-retrieval-stability.json.
/// </summary>
public sealed class Hr002RetrievalStabilityMeasurementTests
{
    private const int RunCount = 10;
    private const string CaseId = "HR-002";
    private const string ExpectedChunkId = "CHK-006";

    [Fact]
    public async Task Hr002_RetrievalScoreStability_MeasureTenConsecutiveRuns()
    {
        if (EvalHostConfiguration.ResolveMode() is not EvalHostMode.DevCloud)
        {
            Assert.Skip("Set EVAL_MODE=DevCloud.");
        }

        var golden = LoadGoldenCase(CaseId);
        using var factory = new Layer1DevCloudWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var searchClient = scope.ServiceProvider.GetRequiredService<SearchClient>();
        var minChunkScore = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentAssist.Application.Configuration.AgentAssistOptions>>()
            .Value.MinChunkScore;

        var runs = new List<object>(RunCount);
        var chk006Scores = new List<double>(RunCount);

        for (var i = 1; i <= RunCount; i++)
        {
            var hits = await SearchTopHitsAsync(searchClient, golden, CancellationToken.None);
            var chk006 = hits.FirstOrDefault(h => h.ChunkId == ExpectedChunkId);
            var top1 = hits.FirstOrDefault();
            var score = chk006?.NormalizedScore ?? 0D;
            chk006Scores.Add(score);

            runs.Add(new
            {
                runIndex = i,
                top1ChunkId = top1?.ChunkId,
                top1Score = top1?.NormalizedScore ?? 0D,
                chk006Score = score,
                chk006PassesMinChunkScore = score >= minChunkScore,
                topHits = hits.Take(4).Select(h => new { h.ChunkId, h.NormalizedScore })
            });

            // Small gap between runs; same process, same credentials, same index.
            await Task.Delay(250, TestContext.Current.CancellationToken);
        }

        var summary = BuildStatistics(chk006Scores, minChunkScore);
        var payload = new
        {
            runStartedUtc = DateTimeOffset.UtcNow,
            caseId = CaseId,
            question = golden.Question,
            expectedChunkId = ExpectedChunkId,
            minChunkScore,
            consecutiveRunCount = RunCount,
            delayBetweenRunsMs = 250,
            evalMode = nameof(EvalHostMode.DevCloud),
            semanticOnly = EvalHostConfiguration.UseSemanticOnlyRetrieval(),
            statistics = summary,
            runs,
            interpretation = Interpret(summary)
        };

        WriteResults(payload);
    }

    private static object BuildStatistics(IReadOnlyList<double> scores, double minChunkScore)
    {
        if (scores.Count is 0)
        {
            return new { count = 0 };
        }

        var min = scores.Min();
        var max = scores.Max();
        var mean = scores.Average();
        var variance = scores.Select(s => (s - mean) * (s - mean)).Average();
        var std = Math.Sqrt(variance);
        var passesAtThreshold = scores.Count(s => s >= minChunkScore);

        return new
        {
            count = scores.Count,
            min,
            max,
            mean,
            std,
            range = max - min,
            passesAtMinChunkScore = passesAtThreshold,
            passRate = (double)passesAtThreshold / scores.Count
        };
    }

    private static string Interpret(dynamic summary)
    {
        var json = JsonSerializer.Serialize(summary);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var range = root.GetProperty("range").GetDouble();
        var std = root.GetProperty("std").GetDouble();
        var min = root.GetProperty("min").GetDouble();
        var max = root.GetProperty("max").GetDouble();

        if (range < 0.005 && std < 0.002)
        {
            return "stable: CHK-006 score did not materially vary across consecutive runs (prior ~0.72 claim was incorrect or a different query/run).";
        }

        if (range >= 0.02 || std >= 0.01)
        {
            return "non-deterministic: CHK-006 semantic score varies across identical consecutive queries; threshold pass/fail can flip between runs.";
        }

        return $"borderline-variance: small but non-zero spread (min={min:F4}, max={max:F4}, std={std:F4}); document pass rate not a single deterministic outcome.";
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

        return hits.OrderByDescending(h => h.NormalizedScore).ToList();
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
        File.WriteAllText(Path.Combine(directory, "hr002-retrieval-stability.json"), json);
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
