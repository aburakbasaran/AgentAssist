using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AgentAssist.Domain;
using AgentAssist.Infrastructure.Azure.OpenAI;
using AgentAssist.Testing;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentAssist.Evaluation.Tests.DevCloud;

/// <summary>
/// Capacity smoke test: AC-001 only, N=1. Fails if judge cannot score (429 after Polly).
/// </summary>
public sealed class Layer2DevCloudMiniValidationTests : IDisposable
{
    private const string CaseId = "AC-001";
    private const int RunsPerCase = 1;
    private static readonly TimeSpan JudgeCallDelay = TimeSpan.FromSeconds(2);
    private const string AgentUserHeader = "X-Agent-User";
    private const string AgentRolesHeader = "X-Agent-Roles";
    private const string AgentLocationHeader = "X-Agent-Location";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions WriteOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly Layer1DevCloudWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Layer2_DevCloud_MiniValidation_AC001_N1()
    {
        if (EvalHostConfiguration.ResolveMode() is not EvalHostMode.DevCloud)
        {
            Assert.Skip("Set EVAL_MODE=DevCloud.");
        }

        var runStartedUtc = DateTimeOffset.UtcNow;
        using var scope = _factory.Services.CreateScope();
        var producerClient = scope.ServiceProvider.GetRequiredService<IChatClient>();
        var judgeClient = ResolveJudgeChatClient(producerClient);
        var chatConfig = new ChatConfiguration(judgeClient);
        var deploymentName = scope.ServiceProvider.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value.ChatDeploymentName;

        var groundednessEvaluator = new GroundednessEvaluator();
        var rtcEvaluator = new RelevanceTruthAndCompletenessEvaluator();
        var golden = LoadGoldenCase(CaseId);
        var ct = TestContext.Current.CancellationToken;

        _factory.TranscriptCollector.Clear();
        ProducerRunOutcome producerOutcome;
        try
        {
            producerOutcome = await Layer2AzureResilience.ExecuteAsync(
                token => ExecuteCaseAsync(golden, token),
                ct);
        }
        catch (Exception ex) when (Layer2AzureResilience.IsRateLimitedFailure(ex))
        {
            WriteMiniResults(runStartedUtc, deploymentName, rateLimited: true, producerRateLimit: true, scored: false, null, null);
            throw new InvalidOperationException("Producer returned HTTP 429 after Polly retries — capacity may still be insufficient.");
        }

        var (statusCode, answer) = producerOutcome;
        statusCode.Should().Be(HttpStatusCode.OK);
        answer.Refused.Should().BeFalse();
        answer.Citations.Count.Should().BeGreaterThan(0);

        var transcript = _factory.TranscriptCollector.Last;
        transcript?.UserMessageSentToModel.Should().Contain("Retrieved chunks:");

        var messages = new List<ChatMessage> { new(ChatRole.User, transcript!.UserMessageSentToModel!) };
        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, answer.AnswerText));
        List<EvaluationContext> groundednessContext = [new GroundednessEvaluatorContext(transcript.UserMessageSentToModel!)];

        await Task.Delay(JudgeCallDelay, ct);
        EvaluationResult groundednessResult;
        try
        {
            groundednessResult = await Layer2AzureResilience.ExecuteAsync(
                token => groundednessEvaluator.EvaluateAsync(messages, modelResponse, chatConfig, groundednessContext, token),
                ct);
        }
        catch (Exception ex) when (Layer2AzureResilience.IsRateLimitedFailure(ex))
        {
            WriteMiniResults(runStartedUtc, deploymentName, rateLimited: true, producerRateLimit: false, scored: false, null, null);
            throw new InvalidOperationException("Judge GroundednessEvaluator returned HTTP 429 after Polly retries — do not run full Layer 2.");
        }

        await Task.Delay(JudgeCallDelay, ct);
        EvaluationResult rtcResult;
        try
        {
            rtcResult = await Layer2AzureResilience.ExecuteAsync(
                token => rtcEvaluator.EvaluateAsync(messages, modelResponse, chatConfig, additionalContext: null, token),
                ct);
        }
        catch (Exception ex) when (Layer2AzureResilience.IsRateLimitedFailure(ex))
        {
            var g = groundednessResult.Get<NumericMetric>(GroundednessEvaluator.GroundednessMetricName);
            WriteMiniResults(runStartedUtc, deploymentName, rateLimited: true, producerRateLimit: false, scored: false, g.Value, null);
            throw new InvalidOperationException("Judge RTC returned HTTP 429 after Polly retries — do not run full Layer 2.");
        }

        var gMetric = groundednessResult.Get<NumericMetric>(GroundednessEvaluator.GroundednessMetricName);
        var rMetric = rtcResult.Get<NumericMetric>(RelevanceTruthAndCompletenessEvaluator.RelevanceMetricName);
        gMetric.Value.Should().NotBeNull("groundedness must return a numeric score");
        rMetric.Value.Should().NotBeNull("relevance must return a numeric score");

        var elapsed = DateTimeOffset.UtcNow - runStartedUtc;
        WriteMiniResults(
            runStartedUtc,
            deploymentName,
            rateLimited: false,
            producerRateLimit: false,
            scored: true,
            gMetric.Value,
            rMetric.Value,
            elapsed);

        elapsed.Should().BeLessThan(TimeSpan.FromMinutes(10), "mini validation should complete in a few minutes");
    }

    private static IChatClient ResolveJudgeChatClient(IChatClient registered) =>
        registered is TranscriptCapturingChatClient capturing ? capturing.Inner : registered;

    private async ValueTask<ProducerRunOutcome> ExecuteCaseAsync(GoldenSetCase golden, CancellationToken ct)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(AgentUserHeader, "pilot-user");
        client.DefaultRequestHeaders.Add(AgentRolesHeader, string.Join(',', golden.Roles));
        client.DefaultRequestHeaders.Add(AgentLocationHeader, "branch-a");

        var response = await client.PostAsJsonAsync("/api/v1/assistant/query", new { question = golden.Question }, ct);
        Layer2AzureResilience.ThrowIfRetryableHttpStatus(response.StatusCode);
        if (response.StatusCode is not HttpStatusCode.OK)
        {
            return new ProducerRunOutcome(
                response.StatusCode,
                AssistantAnswer.RefusedAnswer(
                    $"HTTP {(int)response.StatusCode}",
                    new RiskAssessment { RiskClass = RiskClass.Low, Reason = "http_error" }));
        }

        var answer = await response.Content.ReadFromJsonAsync<AssistantAnswer>(JsonOptions, ct);
        return new ProducerRunOutcome(response.StatusCode, answer!);
    }

    private sealed record ProducerRunOutcome(HttpStatusCode StatusCode, AssistantAnswer Answer);

    private static GoldenSetCase LoadGoldenCase(string id)
    {
        var path = Path.Combine(AppContext.BaseDirectory, EvaluationHarnessFixture.GoldenSetFile);
        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var item = JsonSerializer.Deserialize<GoldenSetCase>(line, JsonOptions);
            if (item is not null && string.Equals(item.Id, id, StringComparison.Ordinal))
            {
                return item;
            }
        }

        throw new InvalidOperationException($"Case {id} not found.");
    }

    private void WriteMiniResults(
        DateTimeOffset runStartedUtc,
        string deploymentName,
        bool rateLimited,
        bool producerRateLimit,
        bool scored,
        object? groundednessScore,
        object? relevanceScore,
        TimeSpan? elapsed = null)
    {
        var summary = new
        {
            runStartedUtc,
            purpose = "mini_validation_capacity_smoke",
            caseId = CaseId,
            runsPerCase = RunsPerCase,
            judgeModelDeployment = deploymentName,
            rateLimited,
            producerRateLimit,
            totalScoredRuns = scored ? 1 : 0,
            includedInDistribution = scored,
            measurementStatus = scored ? "ölçüldü" : Layer2AzureResilience.NotMeasuredRateLimitStatus,
            elapsedSeconds = elapsed?.TotalSeconds,
            scores = scored
                ? new { groundedness = groundednessScore, relevance = relevanceScore }
                : null
        };

        var directory = ResolveResultsDirectory();
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(summary, WriteOptions);
        File.WriteAllText(Path.Combine(directory, "layer2-mini-ac001-latest.json"), json);
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
