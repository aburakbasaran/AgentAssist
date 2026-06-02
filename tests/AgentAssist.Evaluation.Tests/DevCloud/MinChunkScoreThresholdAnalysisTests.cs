using System.Text.Json;

using AgentAssist.Domain;
using AgentAssist.Infrastructure.Azure.Search;
using AgentAssist.Testing;

using Azure.Search.Documents;
using Azure.Search.Documents.Models;

using Microsoft.Extensions.DependencyInjection;

namespace AgentAssist.Evaluation.Tests.DevCloud;

/// <summary>
/// DevCloud-only retrieval score sweep for AC-* questions. Writes eval/results/minchunk-threshold-analysis.json.
/// </summary>
public sealed class MinChunkScoreThresholdAnalysisTests
{
    private static readonly string[] AcCaseIds = ["AC-001", "AC-002", "AC-003", "AC-004", "AC-005", "AC-006"];
    private static readonly double[] Thresholds = [0.5D, 0.6D, 0.7D];

    [Fact]
    public async Task MinChunkScore_AnalyzeAcRetrieval_WriteResults()
    {
        if (EvalHostConfiguration.ResolveMode() is not EvalHostMode.DevCloud)
        {
            Assert.Skip("Set EVAL_MODE=DevCloud.");
        }

        using var factory = new AgentAssistWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var searchClient = scope.ServiceProvider.GetRequiredService<SearchClient>();

        var cases = AcCaseIds.Select(LoadGoldenCase).ToArray();
        var rows = new List<object>();

        foreach (var golden in cases)
        {
            var filter = AzureSearchFilterBuilder.Build(golden.Roles, documentType: null, location: "branch-a");
            var options = new SearchOptions
            {
                Filter = filter,
                Size = 8,
                IncludeTotalCount = false,
                QueryType = SearchQueryType.Semantic,
                SemanticSearch = new SemanticSearchOptions { SemanticConfigurationName = "agentassist-semantic" }
            };

            var response = await searchClient.SearchAsync<AzureSearchDocument>(golden.Question, options, CancellationToken.None);
            var hits = new List<(string ChunkId, double NormalizedScore)>();
            await foreach (var hit in response.Value.GetResultsAsync())
            {
                if (hit.Document is null)
                {
                    continue;
                }

                var raw = hit.SemanticSearch?.RerankerScore ?? hit.Score ?? 0D;
                var normalized = AzureSearchDocumentMapper.NormalizeScore(raw);
                hits.Add((hit.Document.ChunkId, normalized));
            }

            hits = hits.OrderByDescending(h => h.NormalizedScore).ToList();
            var expectedChunk = ExpectedChunkForCase(golden.Id);
            var top = hits.Take(4).Select(h => new
            {
                h.ChunkId,
                score = h.NormalizedScore,
                passesAt = Thresholds.ToDictionary(
                    t => t.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                    t => h.NormalizedScore >= t)
            }).ToArray();

            var thresholdSummary = Thresholds.ToDictionary(
                t => t.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                t => new
                {
                    expectedTop1Retrieved = hits.FirstOrDefault().ChunkId == expectedChunk && (hits.FirstOrDefault().NormalizedScore >= t),
                    expectedInResultSet = hits.Any(h => h.ChunkId == expectedChunk && h.NormalizedScore >= t)
                });

            rows.Add(new
            {
                caseId = golden.Id,
                question = golden.Question,
                roles = golden.Roles,
                expectedChunkId = expectedChunk,
                topChunks = top,
                top1ChunkId = hits.FirstOrDefault().ChunkId,
                top1IsExpected = hits.FirstOrDefault().ChunkId == expectedChunk,
                thresholdSummary
            });
        }

        var allTop1At07 = rows.Count(r => ((dynamic)r).top1IsExpected);
        var summary = new
        {
            runStartedUtc = DateTimeOffset.UtcNow,
            thresholds = Thresholds,
            recommendedMinChunkScore = RecommendThreshold(rows),
            recommendationRationale = "Prefer the highest threshold where every AC case retrieves the expected chunkId in the top-1 semantic hit and no off-domain chunk (e.g. CHK-001 on unrelated queries) dominates AC targets.",
            acCasesWithExpectedTop1At07 = allTop1At07,
            rows
        };

        WriteResults(summary);
    }

    private static double RecommendThreshold(IReadOnlyList<object> rows)
    {
        foreach (var threshold in Thresholds.OrderDescending())
        {
            var allPass = true;
            foreach (var row in rows)
            {
                var json = JsonSerializer.Serialize(row);
                using var doc = JsonDocument.Parse(json);
                var key = threshold.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                if (!doc.RootElement.GetProperty("thresholdSummary").GetProperty(key).GetProperty("expectedTop1Retrieved").GetBoolean())
                {
                    allPass = false;
                    break;
                }
            }

            if (allPass)
            {
                return threshold;
            }
        }

        return 0.5D;
    }

    private static string ExpectedChunkForCase(string caseId) => caseId switch
    {
        "AC-001" or "AC-006" => "CHK-001",
        "AC-002" => "CHK-004",
        "AC-003" => "CHK-003",
        "AC-004" => "CHK-005",
        "AC-005" => "CHK-002",
        _ => "unknown"
    };

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
        File.WriteAllText(Path.Combine(directory, "minchunk-threshold-analysis.json"), json);
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
}
