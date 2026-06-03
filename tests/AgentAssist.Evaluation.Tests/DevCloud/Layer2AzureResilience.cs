using System.Net;

using Polly;
using Polly.Retry;

namespace AgentAssist.Evaluation.Tests.DevCloud;

/// <summary>
/// Polly v8 retry pipeline for Layer 2 DevCloud producer and judge calls (429 + transient).
/// Eval test harness only — not used in Application/Domain.
/// </summary>
internal static class Layer2AzureResilience
{
    public const string NotMeasuredRateLimitStatus = "ölçülemedi (rate limit)";

    private const int MaxRetryAttempts = 6;

    private static readonly ResiliencePipeline Shared = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = MaxRetryAttempts,
            Delay = TimeSpan.FromSeconds(4),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder().Handle<Exception>(IsRetryable)
        })
        .Build();

    public static ValueTask ExecuteAsync(Func<CancellationToken, ValueTask> action, CancellationToken cancellationToken) =>
        Shared.ExecuteAsync(async ct =>
        {
            await action(ct).ConfigureAwait(false);
        }, cancellationToken);

    public static ValueTask<T> ExecuteAsync<T>(Func<CancellationToken, ValueTask<T>> action, CancellationToken cancellationToken) =>
        Shared.ExecuteAsync(action, cancellationToken);

    public static bool IsRateLimitedFailure(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is Layer2RetryableHttpException retryable && retryable.StatusCode is HttpStatusCode.TooManyRequests)
            {
                return true;
            }

            if (IsRateLimitMessage(current.Message))
            {
                return true;
            }
        }

        return false;
    }

    public static void ThrowIfRetryableHttpStatus(HttpStatusCode statusCode)
    {
        if (statusCode is HttpStatusCode.TooManyRequests
            or HttpStatusCode.RequestTimeout
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout)
        {
            throw new Layer2RetryableHttpException(statusCode);
        }
    }

    private static bool IsRetryable(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is Layer2RetryableHttpException)
            {
                return true;
            }

            if (current is HttpRequestException or TaskCanceledException)
            {
                return true;
            }

            if (IsRateLimitMessage(current.Message))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRateLimitMessage(string message) =>
        message.Contains("429", StringComparison.Ordinal)
        || message.Contains("too_many_requests", StringComparison.OrdinalIgnoreCase)
        || message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase);
}

internal sealed class Layer2RetryableHttpException(HttpStatusCode statusCode) : Exception($"Retryable HTTP {(int)statusCode} ({statusCode})")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
