using System.Collections.Generic;

using AgentAssist.Api.Logging;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentAssist.Api.IntegrationTests;

/// <summary>
/// Verifies that the production-only logger decorator actually rewrites the formatter output before it reaches the inner logger pipeline. Documents the defence-in-depth contract — even if a future caller accidentally interpolates a raw <c>Question</c> / <c>AnswerText</c> / <c>Content</c> field into a log line, the redacted form is what every downstream provider observes.
/// </summary>
public sealed class SensitiveDataRedactingLoggerFactoryTests
{
    [Theory]
    [InlineData(
        "Handled query Question=\"how do I reset password\" mode=DevCloud",
        "Handled query Question=\"[REDACTED]\" mode=DevCloud")]
    [InlineData(
        "Audit AnswerText=\"You can reset by ...\" Confidence=0.92",
        "Audit AnswerText=\"[REDACTED]\" Confidence=0.92")]
    [InlineData(
        "Chat trace Content=\"hello world\" tokens=42",
        "Chat trace Content=\"[REDACTED]\" tokens=42")]
    [InlineData(
        "no sensitive markers here",
        "no sensitive markers here")]
    public void Redact_ReplacesSensitiveFields(string input, string expected)
    {
        var actual = SensitiveDataRedactingLoggerFactory.Redact(input);
        actual.Should().Be(expected);
    }

    [Fact]
    public void DecoratedLogger_ForwardsRedactedFormatterOutputToInnerLogger()
    {
        var captured = new List<string>();
        var captureFactory = new CapturingLoggerFactory(captured);
        using var redactingFactory = new SensitiveDataRedactingLoggerFactory(captureFactory);

        var logger = redactingFactory.CreateLogger("Test.Category");

        logger.LogInformation("user request Question=\"top secret\" mode=DevCloud");

        captured.Should().ContainSingle();
        captured[0].Should().Be("user request Question=\"[REDACTED]\" mode=DevCloud");
    }

    private sealed class CapturingLoggerFactory(List<string> sink) : ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(sink);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(List<string> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            sink.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
