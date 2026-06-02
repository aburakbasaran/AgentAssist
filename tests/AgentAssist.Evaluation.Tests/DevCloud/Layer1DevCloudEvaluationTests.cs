using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AgentAssist.Application.Abstractions;
using AgentAssist.Application.Configuration;
using AgentAssist.Domain;
using AgentAssist.Infrastructure.Azure.OpenAI;
using AgentAssist.Testing;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentAssist.Evaluation.Tests.DevCloud;

/// <summary>
/// DevCloud-only test host with transcript capture decorator. Never used when <c>EVAL_MODE</c> is unset.
/// </summary>
public sealed class Layer1DevCloudWebApplicationFactory : WebApplicationFactory<Program>
{
    public ChatTranscriptCollector TranscriptCollector { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        EvalHostConfiguration.ConfigureWebHost(builder);
        _ = builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(TranscriptCollector);
            EvalChatClientRegistration.WrapChatClientWithTranscriptCapture(services);
        });
    }
}

/// <summary>
/// Katman 1: runs the full golden set against real Azure when <c>EVAL_MODE=DevCloud</c>. Skipped in CI (unset <c>EVAL_MODE</c>).
/// </summary>
public sealed class Layer1DevCloudEvaluationTests : IDisposable
{
    private const string AgentUserHeader = "X-Agent-User";
    private const string AgentRolesHeader = "X-Agent-Roles";
    private const string AgentLocationHeader = "X-Agent-Location";
    private const string ProbeCaseId = "AC-001";
    private const string NoSourceUserMessage = "Bu soruyu yanıtlamak için yeterli kaynak bulunamadı.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions WriteOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly Layer1DevCloudWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Layer1_DevCloud_ConnectivityProbe_AC001_ReturnsGroundedCitation()
    {
        SkipUnlessDevCloud();
        var probeCase = LoadGoldenCase(ProbeCaseId);
        await AssertDevCloudConnectivityAsync(probeCase);
    }

    [Fact]
    public async Task Layer1_DevCloud_RunGoldenSet_WriteResultsAndTranscripts()
    {
        SkipUnlessDevCloud();
        await AssertDevCloudConnectivityAsync(LoadGoldenCase(ProbeCaseId));

        var readyHealthy = await IsReadyHealthHealthyAsync();
        const bool connectivityProbePassed = true;
        var cases = LoadAllGoldenCases();
        var runStartedUtc = DateTimeOffset.UtcNow;
        var chatDeployment = ResolveChatDeploymentName();
        var minChunkScore = ResolveMinChunkScore();
        var caseResults = new List<Layer1CaseResult>(cases.Count);
        var transcripts = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var golden in cases)
        {
            _factory.TranscriptCollector.Clear();
            var (statusCode, answer) = await ExecuteCaseAsync(golden, CancellationToken.None);
            var kind = Layer1OutcomeClassifier.Classify(
                golden,
                answer,
                (int)statusCode,
                readyHealthy,
                connectivityProbePassed);
            var pass = Layer1OutcomeClassifier.PassForCategory(golden, kind);

            caseResults.Add(new Layer1CaseResult(
                golden.Id,
                golden.Category,
                golden.Question,
                golden.ExpectedRefused,
                golden.ExpectedEscalation,
                (int)statusCode,
                answer.Refused,
                answer.Citations.Count,
                answer.EscalationRequired,
                answer.RefusalReason,
                kind,
                pass));

            if (ShouldCaptureTranscript(golden.Id))
            {
                transcripts[golden.Id] = BuildTranscript(golden, answer, _factory.TranscriptCollector.Last);
            }
        }

        var summary = new
        {
            runStartedUtc,
            evalMode = nameof(EvalHostMode.DevCloud),
            semanticOnly = EvalHostConfiguration.UseSemanticOnlyRetrieval(),
            chatDeploymentName = chatDeployment,
            minChunkScore,
            azureTenantIdRequired = true,
            readyHealthHealthy = readyHealthy,
            connectivityProbePassed,
            connectivityProbeCaseId = ProbeCaseId,
            totalCases = cases.Count,
            passCount = caseResults.Count(c => c.Pass),
            caseResults = caseResults.Select(c => new
            {
                id = c.Id,
                category = c.Category,
                question = c.Question,
                expectedRefused = c.ExpectedRefused,
                expectedEscalation = c.ExpectedEscalation,
                httpStatusCode = c.HttpStatusCode,
                actualRefused = c.ActualRefused,
                actualCitationCount = c.ActualCitationCount,
                actualEscalationRequired = c.ActualEscalationRequired,
                actualRefusalReason = c.ActualRefusalReason,
                outcomeKind = c.OutcomeKind.ToString(),
                pass = c.Pass
            }),
            transcripts,
            refusalPointsObserved = BuildRefusalPointSummary(caseResults)
        };

        WriteLayer1Results(summary, runStartedUtc);

        connectivityProbePassed.Should().BeTrue("AC-001 probe must pass before Katman 1 results are valid");

        var infrastructureFailures = caseResults
            .Where(c => c.OutcomeKind is Layer1OutcomeKind.InvalidInfrastructureRefusal or Layer1OutcomeKind.AzureUnavailable)
            .Select(c => c.Id)
            .ToArray();
        infrastructureFailures.Should().BeEmpty(
            $"cases {string.Join(", ", infrastructureFailures)} indicate Azure connectivity failure (not index/golden gaps)");

        transcripts.Should().ContainKey("NS-001");
        var nsTranscript = transcripts["NS-001"];
        nsTranscript.ToString().Should().Contain("llmInvoked");

        caseResults.Count(c => c.Category == "answerable_with_citation"
            && c.OutcomeKind == Layer1OutcomeKind.GroundedWithCitations)
            .Should()
            .BeGreaterThanOrEqualTo(2, "at least two AC-* cases must return real citations when Azure is connected");
    }

    private async Task<bool> RunConnectivityProbeAsync()
    {
        try
        {
            await AssertDevCloudConnectivityAsync(LoadGoldenCase(ProbeCaseId));
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task Layer1_DevCloud_InvalidEndpoint_Returns503NotRefusal()
    {
        SkipUnlessDevCloud();

        var previousEndpoint = Environment.GetEnvironmentVariable("AzureSearch__Endpoint");
        try
        {
            Environment.SetEnvironmentVariable("AzureSearch__Endpoint", "https://invalid-agentassist-eval-host.example");
            using var invalidFactory = new Layer1DevCloudWebApplicationFactory();
            using var client = invalidFactory.CreateClient();

            var response = await client.GetAsync("/health/ready", CancellationToken.None);
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable,
                "upstream Azure failure must surface as 503 on readiness, not a structured assistant refusal");
        }
        finally
        {
            Environment.SetEnvironmentVariable("AzureSearch__Endpoint", previousEndpoint);
        }
    }

    private async Task AssertDevCloudConnectivityAsync(GoldenSetCase probeCase)
    {
        using var scope = _factory.Services.CreateScope();
        var search = scope.ServiceProvider.GetRequiredService<IKnowledgeSearchService>();
        search.GetType().Name.Should().Be("AzureSearchKnowledgeService", "DevCloud eval must register Azure Search, not Mock");

        var (statusCode, answer) = await ExecuteCaseAsync(probeCase, CancellationToken.None);
        statusCode.Should().Be(HttpStatusCode.OK);
        answer.Refused.Should().BeFalse($"connectivity probe {probeCase.Id} must return a grounded citation, not no-source refusal");
        answer.Citations.Count.Should().BeGreaterThan(0, "probe must prove Azure Search + OpenAI path works for answerable questions");

        var readyHealthy = await IsReadyHealthHealthyAsync();
        readyHealthy.Should().BeTrue(
            "/health/ready should be Healthy when Search/OpenAI/SQL are reachable; if this fails but AC-001 passed, SQL audit may be the outlier — check AzureSql:ConnectionString");
    }

    private async Task<bool> IsReadyHealthHealthyAsync()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/ready", CancellationToken.None);
        return response.StatusCode is HttpStatusCode.OK;
    }

    private async Task<(HttpStatusCode StatusCode, AssistantAnswer Answer)> ExecuteCaseAsync(
        GoldenSetCase golden,
        CancellationToken ct)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(AgentUserHeader, "pilot-user");
        client.DefaultRequestHeaders.Add(AgentRolesHeader, string.Join(',', golden.Roles));
        client.DefaultRequestHeaders.Add(AgentLocationHeader, "branch-a");

        var response = await client.PostAsJsonAsync("/api/v1/assistant/query", new { question = golden.Question }, ct);
        if (response.StatusCode is not HttpStatusCode.OK)
        {
            return (response.StatusCode, AssistantAnswer.RefusedAnswer(
                $"HTTP {(int)response.StatusCode}",
                new RiskAssessment { RiskClass = RiskClass.Low, Reason = "http_error" }));
        }

        var answer = await response.Content.ReadFromJsonAsync<AssistantAnswer>(JsonOptions, ct);
        return (response.StatusCode, answer ?? throw new InvalidOperationException($"Case {golden.Id} produced no answer."));
    }

    private static void SkipUnlessDevCloud()
    {
        if (EvalHostConfiguration.ResolveMode() is not EvalHostMode.DevCloud)
        {
            Assert.Skip("Set EVAL_MODE=DevCloud to run Katman 1 DevCloud evaluation.");
        }
    }

    private static bool ShouldCaptureTranscript(string caseId) =>
        caseId is "NS-001" or "AD-002";

    private static object BuildTranscript(
        GoldenSetCase golden,
        AssistantAnswer answer,
        ChatTranscriptRecord? llmCapture)
    {
        return new
        {
            caseId = golden.Id,
            category = golden.Category,
            question = golden.Question,
            finalAnswer = new
            {
                answer.Refused,
                answer.AnswerText,
                answer.RefusalReason,
                citationIds = answer.Citations.Select(c => c.ChunkId).ToArray()
            },
            llmInvoked = llmCapture?.LlmInvoked ?? false,
            userMessageSentToModel = llmCapture?.UserMessageSentToModel,
            rawModelResponseText = llmCapture?.RawModelResponseText,
            capturedAtUtc = llmCapture?.CapturedAtUtc,
            note = llmCapture?.LlmInvoked == true
                ? "LLM invoked; user message includes retrieved chunk text as sent to the model."
                : "Orchestrator refused before LLM (no-source path); no model transcript."
        };
    }

    private static object BuildRefusalPointSummary(IReadOnlyList<Layer1CaseResult> caseResults) => new
    {
        noSourceOrchestrator = caseResults.Any(c =>
            c.ActualRefused && string.Equals(c.ActualRefusalReason, NoSourceUserMessage, StringComparison.Ordinal)),
        modelSelfRefusal = caseResults.Any(c =>
            c.ActualRefused
            && c.ActualRefusalReason is not null
            && c.ActualRefusalReason is not NoSourceUserMessage
            && c.ActualRefusalReason is not "model_returned_malformed_response"
            && c.ActualRefusalReason is not "model_returned_invalid_citation"),
        malformedResponse = caseResults.Any(c => c.ActualRefusalReason == "model_returned_malformed_response"),
        invalidCitation = caseResults.Any(c => c.ActualRefusalReason == "model_returned_invalid_citation"),
        azure503Observed = caseResults.Any(c => c.HttpStatusCode == 503)
    };

    private string? ResolveChatDeploymentName()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value.ChatDeploymentName;
    }

    private double ResolveMinChunkScore()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IOptions<AgentAssistOptions>>().Value.MinChunkScore;
    }

    private static void WriteLayer1Results(object summary, DateTimeOffset runStartedUtc)
    {
        var directory = ResolveResultsDirectory();
        Directory.CreateDirectory(directory);
        var stamp = runStartedUtc.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var path = Path.Combine(directory, $"layer1-devcloud-{stamp}.json");
        var json = JsonSerializer.Serialize(summary, WriteOptions);
        File.WriteAllText(path, json);
        File.WriteAllText(Path.Combine(directory, "layer1-devcloud-latest.json"), json);
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

    private static GoldenSetCase LoadGoldenCase(string id)
    {
        var match = LoadAllGoldenCases().FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));
        return match ?? throw new InvalidOperationException($"Golden case {id} not found.");
    }

    private static IReadOnlyList<GoldenSetCase> LoadAllGoldenCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, EvaluationHarnessFixture.GoldenSetFile);
        var lines = File.ReadAllLines(path);
        var cases = new List<GoldenSetCase>(lines.Length);
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
}
