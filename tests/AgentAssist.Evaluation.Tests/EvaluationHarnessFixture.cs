using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AgentAssist.Domain;

using AgentAssist.Testing;

namespace AgentAssist.Evaluation.Tests;

/// <summary>
/// Class fixture for the production-pilot evaluation harness. Loads the golden set once, executes every case against the in-process Mock-mode pilot API, caches the results so per-case <c>[Theory]</c> rows do not re-issue HTTP calls, and writes a deterministic JSON summary to <c>eval/results/</c> for CI artefact upload.
/// </summary>
public sealed class EvaluationHarnessFixture : IDisposable
{
    public const string GoldenSetFile = "golden-set.production-pilot.jsonl";
    public const string AgentUserHeader = "X-Agent-User";
    public const string AgentRolesHeader = "X-Agent-Roles";
    public const string AgentLocationHeader = "X-Agent-Location";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions WriteOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly AgentAssistWebApplicationFactory _factory = new();
    private readonly Lazy<IReadOnlyList<EvaluationOutcome>> _outcomes;
    private readonly TimeProvider _timeProvider;

    public EvaluationHarnessFixture()
        : this(TimeProvider.System)
    {
    }

    internal EvaluationHarnessFixture(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        Cases = LoadGoldenSet();
        _outcomes = new Lazy<IReadOnlyList<EvaluationOutcome>>(ExecuteAllCases, isThreadSafe: true);
    }

    public IReadOnlyList<GoldenSetCase> Cases { get; }

    public IReadOnlyList<EvaluationOutcome> Outcomes => _outcomes.Value;

    public EvaluationOutcome GetOutcome(string id) =>
        Outcomes.First(outcome => string.Equals(outcome.Case.Id, id, StringComparison.Ordinal));

    public void Dispose()
    {
        _factory.Dispose();
    }

    private IReadOnlyList<EvaluationOutcome> ExecuteAllCases()
    {
        var outcomes = new List<EvaluationOutcome>(capacity: Cases.Count);
        foreach (var golden in Cases)
        {
            var answer = ExecuteAsync(golden, CancellationToken.None).GetAwaiter().GetResult();
            outcomes.Add(new EvaluationOutcome(golden, answer));
        }

        WriteResults(outcomes, _timeProvider);
        return outcomes;
    }

    private async Task<AssistantAnswer> ExecuteAsync(GoldenSetCase golden, CancellationToken ct)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(AgentUserHeader, "pilot-user");
        client.DefaultRequestHeaders.Add(AgentRolesHeader, string.Join(',', golden.Roles));
        client.DefaultRequestHeaders.Add(AgentLocationHeader, "branch-a");

        var response = await client.PostAsJsonAsync("/api/v1/assistant/query", new { question = golden.Question }, ct);
        if (response.StatusCode is not HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"Case {golden.Id} returned HTTP {(int)response.StatusCode}.");
        }

        var answer = await response.Content.ReadFromJsonAsync<AssistantAnswer>(JsonOptions, ct);
        return answer ?? throw new InvalidOperationException($"Case {golden.Id} produced no answer.");
    }

    private static IReadOnlyList<GoldenSetCase> LoadGoldenSet()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var path = Path.Combine(baseDirectory, GoldenSetFile);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Evaluation golden set not found at {path}.", path);
        }

        var lines = File.ReadAllLines(path);
        var cases = new List<GoldenSetCase>(capacity: lines.Length);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var item = JsonSerializer.Deserialize<GoldenSetCase>(line, JsonOptions);
            if (item is not null)
            {
                cases.Add(item);
            }
        }

        return cases;
    }

    private static void WriteResults(IReadOnlyList<EvaluationOutcome> outcomes, TimeProvider timeProvider)
    {
        try
        {
            var resultsDirectory = ResolveResultsDirectory();
            Directory.CreateDirectory(resultsDirectory);
            var runStartedUtc = timeProvider.GetUtcNow();
            var stamp = runStartedUtc.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            var fileName = $"production-pilot-evaluation-{stamp}.json";
            var fullPath = Path.Combine(resultsDirectory, fileName);

            var summary = new
            {
                runStartedUtc,
                evalMode = EvalHostConfiguration.ResolveMode().ToString(),
                totalCases = outcomes.Count,
                refusedCount = outcomes.Count(o => o.Answer.Refused),
                groundedCount = outcomes.Count(o => !o.Answer.Refused),
                escalationCount = outcomes.Count(o => o.Answer.EscalationRequired),
                cases = outcomes.Select(o => new
                {
                    id = o.Case.Id,
                    category = o.Case.Category,
                    roles = o.Case.Roles,
                    expectedRefused = o.Case.ExpectedRefused,
                    actualRefused = o.Answer.Refused,
                    actualCitationCount = o.Answer.Citations.Count,
                    actualEscalationRequired = o.Answer.EscalationRequired,
                    actualRiskClass = o.Answer.RiskClass.ToString(),
                    actualConfidence = o.Answer.ConfidenceLevel.ToString(),
                    actualRefusalReason = o.Answer.RefusalReason
                })
            };

            var json = JsonSerializer.Serialize(summary, WriteOptions);
            File.WriteAllText(fullPath, json);

            var latestPath = Path.Combine(resultsDirectory, "production-pilot-evaluation-latest.json");
            File.WriteAllText(latestPath, json);
        }
        catch
        {
            // reason: writing the artefact is best-effort; failure (e.g. read-only sandbox) must not break the test run.
        }
    }

    private static string ResolveResultsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 10 && directory is not null; depth++)
        {
            var candidate = Path.Combine(directory.FullName, "eval", "results");
            if (Directory.Exists(Path.Combine(directory.FullName, "eval")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "eval", "results");
    }
}

public sealed record EvaluationOutcome(GoldenSetCase Case, AssistantAnswer Answer);
