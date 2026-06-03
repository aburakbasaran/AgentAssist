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
/// Katman 2: quality metrics (groundedness + RTC) on DevCloud grounded cases with N-run distributions.
/// Serial runs only; producer + judge wrapped in Polly v8 retry (429/transient). Cases that exhaust retries
/// on rate limit are marked <see cref="Layer2AzureResilience.NotMeasuredRateLimitStatus"/> and excluded from distributions.
/// </summary>
public sealed class Layer2DevCloudQualityEvaluationTests : IDisposable
{
    private const int RunsPerCase = 3;
    private static readonly TimeSpan MaxSuiteDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ProducerRunDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan JudgeCallDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CaseDelay = TimeSpan.FromSeconds(3);
    private const string AgentUserHeader = "X-Agent-User";
    private const string AgentRolesHeader = "X-Agent-Roles";
    private const string AgentLocationHeader = "X-Agent-Location";

    private static readonly string[] GroundedCaseIds =
    [
        "AC-001", "AC-002", "AC-003", "AC-004", "AC-005", "AC-006",
        "HR-001", "HR-003"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions WriteOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly Layer1DevCloudWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Layer2_DevCloud_QualityMetrics_WriteResults()
    {
        if (EvalHostConfiguration.ResolveMode() is not EvalHostMode.DevCloud)
        {
            Assert.Skip("Set EVAL_MODE=DevCloud.");
        }

        using var scope = _factory.Services.CreateScope();
        var producerClient = scope.ServiceProvider.GetRequiredService<IChatClient>();
        var judgeClient = ResolveJudgeChatClient(producerClient);
        var chatConfig = new ChatConfiguration(judgeClient);
        var deploymentName = scope.ServiceProvider.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value.ChatDeploymentName;

        var groundednessEvaluator = new GroundednessEvaluator();
        var rtcEvaluator = new RelevanceTruthAndCompletenessEvaluator();

        var runStartedUtc = DateTimeOffset.UtcNow;
        var stamp = runStartedUtc.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var caseResults = new List<object>();
        var totalScoredRuns = 0;
        var totalRateLimitFailures = 0;
        string runStatus = "in_progress";

        for (var caseIndex = 0; caseIndex < GroundedCaseIds.Length; caseIndex++)
        {
            var elapsed = DateTimeOffset.UtcNow - runStartedUtc;
            if (elapsed > MaxSuiteDuration)
            {
                runStatus = "aborted_timeout";
                WriteResults(
                    BuildSummary(
                        runStartedUtc,
                        deploymentName,
                        caseResults,
                        totalScoredRuns,
                        totalRateLimitFailures,
                        runStatus,
                        elapsed),
                    stamp,
                    final: false);
                throw new TimeoutException(
                    $"Layer 2 exceeded {MaxSuiteDuration.TotalMinutes} minutes (elapsed {elapsed.TotalMinutes:F1} min). Partial results in layer2-quality-latest.json.");
            }

            if (caseIndex > 0)
            {
                await Task.Delay(CaseDelay, TestContext.Current.CancellationToken);
            }

            var caseId = GroundedCaseIds[caseIndex];
            var golden = LoadGoldenCase(caseId);
            var runRows = new List<object>();
            var groundednessScores = new List<double>();
            var relevanceScores = new List<double>();
            var truthScores = new List<double>();
            var completenessScores = new List<double>();
            var caseRateLimitFailures = 0;
            var attemptedRuns = 0;

            for (var runIndex = 1; runIndex <= RunsPerCase; runIndex++)
            {
                if (runIndex > 1)
                {
                    await Task.Delay(ProducerRunDelay, TestContext.Current.CancellationToken);
                }

                attemptedRuns++;
                _factory.TranscriptCollector.Clear();

                ProducerRunOutcome producerOutcome;
                try
                {
                    producerOutcome = await Layer2AzureResilience.ExecuteAsync(
                        ct => ExecuteCaseAsync(golden, ct),
                        TestContext.Current.CancellationToken);
                }
                catch (Exception ex) when (Layer2AzureResilience.IsRateLimitedFailure(ex))
                {
                    caseRateLimitFailures++;
                    totalRateLimitFailures++;
                    runRows.Add(RateLimitRunRow(runIndex, "Producer HTTP call exhausted Polly retries (HTTP 429)"));
                    continue;
                }

                var (statusCode, answer) = producerOutcome;
                if (statusCode is not HttpStatusCode.OK || answer.Refused || answer.Citations.Count is 0)
                {
                    runRows.Add(new
                    {
                        runIndex,
                        skipped = true,
                        skipReason = answer.Refused ? answer.RefusalReason : $"HTTP {(int)statusCode} or no citations",
                        notMeasuredRateLimit = false,
                        groundedness = (object?)null,
                        relevanceRtc = (object?)null
                    });
                    continue;
                }

                var transcript = _factory.TranscriptCollector.Last;
                if (transcript?.UserMessageSentToModel is null
                    || !transcript.UserMessageSentToModel.Contains("Retrieved chunks:", StringComparison.Ordinal))
                {
                    runRows.Add(new
                    {
                        runIndex,
                        skipped = true,
                        skipReason = "No transcript with Retrieved chunks (context drift guard)",
                        notMeasuredRateLimit = false,
                        groundedness = (object?)null,
                        relevanceRtc = (object?)null
                    });
                    continue;
                }

                var messages = new List<ChatMessage>
                {
                    new(ChatRole.User, transcript.UserMessageSentToModel)
                };
                var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, answer.AnswerText));
                var groundingContext = transcript.UserMessageSentToModel;
                List<EvaluationContext> groundednessContext =
                [
                    new GroundednessEvaluatorContext(groundingContext)
                ];

                await Task.Delay(JudgeCallDelay, TestContext.Current.CancellationToken);
                EvaluationResult? groundednessResult;
                try
                {
                    groundednessResult = await Layer2AzureResilience.ExecuteAsync(
                        ct => groundednessEvaluator.EvaluateAsync(
                            messages,
                            modelResponse,
                            chatConfig,
                            groundednessContext,
                            ct),
                        TestContext.Current.CancellationToken);
                }
                catch (Exception ex) when (Layer2AzureResilience.IsRateLimitedFailure(ex))
                {
                    caseRateLimitFailures++;
                    totalRateLimitFailures++;
                    runRows.Add(RateLimitRunRow(runIndex, "Judge groundedness exhausted Polly retries (HTTP 429)"));
                    continue;
                }

                await Task.Delay(JudgeCallDelay, TestContext.Current.CancellationToken);
                EvaluationResult? rtcResult;
                try
                {
                    rtcResult = await Layer2AzureResilience.ExecuteAsync(
                        ct => rtcEvaluator.EvaluateAsync(
                            messages,
                            modelResponse,
                            chatConfig,
                            additionalContext: null,
                            ct),
                        TestContext.Current.CancellationToken);
                }
                catch (Exception ex) when (Layer2AzureResilience.IsRateLimitedFailure(ex))
                {
                    caseRateLimitFailures++;
                    totalRateLimitFailures++;
                    runRows.Add(new
                    {
                        runIndex,
                        skipped = true,
                        skipReason = Layer2AzureResilience.NotMeasuredRateLimitStatus,
                        notMeasuredRateLimit = true,
                        detail = "Judge RTC exhausted Polly retries (HTTP 429)",
                        groundedness = CaptureMetricSnapshot(
                            groundednessResult!.Get<NumericMetric>(GroundednessEvaluator.GroundednessMetricName)),
                        relevanceRtc = (object?)null
                    });
                    continue;
                }

                var gMetric = groundednessResult!.Get<NumericMetric>(GroundednessEvaluator.GroundednessMetricName);
                var rMetric = rtcResult!.Get<NumericMetric>(RelevanceTruthAndCompletenessEvaluator.RelevanceMetricName);
                var tMetric = rtcResult.Get<NumericMetric>(RelevanceTruthAndCompletenessEvaluator.TruthMetricName);
                var cMetric = rtcResult.Get<NumericMetric>(RelevanceTruthAndCompletenessEvaluator.CompletenessMetricName);

                TryAddNumericScore(gMetric.Value, groundednessScores);
                TryAddNumericScore(rMetric.Value, relevanceScores);
                TryAddNumericScore(tMetric.Value, truthScores);
                TryAddNumericScore(cMetric.Value, completenessScores);

                runRows.Add(new
                {
                    runIndex,
                    skipped = false,
                    contextSource = "TranscriptCapturingChatClient.UserMessageSentToModel",
                    contextContainsRetrievedChunks = true,
                    contextLength = groundingContext.Length,
                    producerAnswerText = answer.AnswerText,
                    citationIds = answer.Citations.Select(c => c.ChunkId).ToArray(),
                    groundedness = CaptureMetricSnapshot(gMetric),
                    relevanceRtc = new
                    {
                        relevance = CaptureMetricSnapshot(rMetric),
                        truth = CaptureMetricSnapshot(tMetric),
                        completeness = CaptureMetricSnapshot(cMetric)
                    }
                });
            }

            var runsScored = groundednessScores.Count;
            totalScoredRuns += runsScored;

            string? measurementStatus = null;
            var includedInDistribution = runsScored > 0;
            if (runsScored is 0 && caseRateLimitFailures > 0 && caseRateLimitFailures == attemptedRuns)
            {
                measurementStatus = Layer2AzureResilience.NotMeasuredRateLimitStatus;
                includedInDistribution = false;
            }
            else if (runsScored is 0)
            {
                measurementStatus = "ölçülemedi (diğer atlama)";
                includedInDistribution = false;
            }
            else
            {
                measurementStatus = "ölçüldü";
            }

            object? distributions = includedInDistribution
                ? new
                {
                    groundedness = ToDistributionDto(Layer2DistributionStatistics.Summarize(groundednessScores)),
                    relevance = ToDistributionDto(Layer2DistributionStatistics.Summarize(relevanceScores)),
                    truth = ToDistributionDto(Layer2DistributionStatistics.Summarize(truthScores)),
                    completeness = ToDistributionDto(Layer2DistributionStatistics.Summarize(completenessScores))
                }
                : null;

            caseResults.Add(new
            {
                caseId,
                question = golden.Question,
                runsRequested = RunsPerCase,
                runsScored,
                rateLimitFailures = caseRateLimitFailures,
                measurementStatus,
                includedInDistribution,
                runs = runRows,
                distributions
            });

            WriteResults(
                BuildSummary(
                    runStartedUtc,
                    deploymentName,
                    caseResults,
                    totalScoredRuns,
                    totalRateLimitFailures,
                    runStatus,
                    DateTimeOffset.UtcNow - runStartedUtc),
                stamp,
                final: false);
        }

        runStatus = "completed";
        var completedElapsed = DateTimeOffset.UtcNow - runStartedUtc;
        var finalSummary = BuildSummary(
            runStartedUtc,
            deploymentName,
            caseResults,
            totalScoredRuns,
            totalRateLimitFailures,
            runStatus,
            completedElapsed);

        WriteResults(finalSummary, stamp, final: true);

        caseResults.Should().NotBeEmpty();
        caseResults.Count.Should().Be(GroundedCaseIds.Length);
        totalScoredRuns.Should().BeGreaterThan(0, "capacity-30 run should score at least one run");
        completedElapsed.Should().BeLessThan(MaxSuiteDuration);
    }

    private static object BuildSummary(
        DateTimeOffset runStartedUtc,
        string deploymentName,
        List<object> caseResults,
        int totalScoredRuns,
        int totalRateLimitFailures,
        string runStatus,
        TimeSpan elapsed) => new
    {
        runStartedUtc,
        runStatus,
        casesCompleted = caseResults.Count,
        casesTotal = GroundedCaseIds.Length,
        elapsedSeconds = elapsed.TotalSeconds,
        evalMode = nameof(EvalHostMode.DevCloud),
        layer = 2,
        runsPerCase = RunsPerCase,
        executionMode = "serial",
        incrementalWriteNote = "layer2-quality-latest.json updated after each case completes",
        resilience = new
        {
            library = "Polly",
            version = "8.5.2",
            maxRetryAttempts = 6,
            backoff = "exponential with jitter",
            appliesTo = new[] { "producer /api/v1/assistant/query", "judge GroundednessEvaluator", "judge RelevanceTruthAndCompletenessEvaluator" }
        },
        groundedCaseIds = GroundedCaseIds,
        judgeModelDeployment = deploymentName,
        judgeSameAsProducer = true,
        selfGradingBiasNote = "Judge and producer share the same IChatClient deployment (self-grading); report scores without inflating.",
        contextSourceConfirmation = "Groundedness grounding context = exact UserMessageSentToModel from TranscriptCapturingChatClient (includes Retrieved chunks block). No second SearchAsync.",
        rateLimitNote = "Runs/cases marked ölçülemedi (rate limit) when Polly retries are exhausted on HTTP 429; those scores are not written into distributions.",
        totalScoredRuns,
        totalRateLimitFailures,
        evaluators = new[]
        {
            nameof(GroundednessEvaluator),
            nameof(RelevanceTruthAndCompletenessEvaluator)
        },
        tokenUsageEstimate = new
        {
            note = "Per scored run: ≈2 judge LLM calls + 1 producer call; exact token counts not on EvaluationResult.Metadata in this harness.",
            scoredRunsApprox = totalScoredRuns,
            judgeCallsApprox = totalScoredRuns * 2,
            producerCallsApprox = totalScoredRuns
        },
        cases = caseResults
    };

    private static void TryAddNumericScore(object? value, List<double> scores)
    {
        if (value is null)
        {
            return;
        }

        scores.Add(Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static object RateLimitRunRow(int runIndex, string detail) => new
    {
        runIndex,
        skipped = true,
        skipReason = Layer2AzureResilience.NotMeasuredRateLimitStatus,
        notMeasuredRateLimit = true,
        detail,
        groundedness = (object?)null,
        relevanceRtc = (object?)null
    };

    private static IChatClient ResolveJudgeChatClient(IChatClient registered)
    {
        if (registered is TranscriptCapturingChatClient capturing)
        {
            return capturing.Inner;
        }

        return registered;
    }

    private static object CaptureMetricSnapshot(NumericMetric metric) => new
    {
        name = metric.Name,
        value = metric.Value,
        reason = metric.Reason,
        interpretationFailed = metric.Interpretation?.Failed,
        interpretationRating = metric.Interpretation?.Rating,
        interpretationReason = metric.Interpretation?.Reason
    };

    private static object ToDistributionDto(DistributionSummary d) => new
    {
        count = d.Count,
        min = d.Min,
        max = d.Max,
        mean = d.Mean,
        std = d.Std,
        confidence95Lower = d.Confidence95Lower,
        confidence95Upper = d.Confidence95Upper
    };

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
        return new ProducerRunOutcome(
            response.StatusCode,
            answer ?? throw new InvalidOperationException($"Case {golden.Id} produced no answer."));
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

    private static void WriteResults(object summary, string stamp, bool final)
    {
        var directory = ResolveResultsDirectory();
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(summary, WriteOptions);
        File.WriteAllText(Path.Combine(directory, "layer2-quality-latest.json"), json);
        if (final)
        {
            File.WriteAllText(Path.Combine(directory, $"layer2-quality-{stamp}.json"), json);
        }
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
