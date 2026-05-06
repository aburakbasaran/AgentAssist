using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AgentAssist.Api.Logging;

/// <summary>
/// Production-only <see cref="ILoggerFactory"/> decorator that wraps every <see cref="ILogger"/> the host produces. Each wrapped logger intercepts the formatter delegate, applies <see cref="Redact"/>, and forwards the redacted message to the inner logger so every downstream provider (Application Insights, Console, file, …) sees the redacted text instead of the raw one.
///
/// The handler / audit pipeline is independently designed to never log raw question or answer text (audit persists a SHA-256 hash and a 200-char preview only). This decorator is therefore intentionally redundant defence-in-depth: a developer who later adds <c>logger.LogInformation("Question={Question}", question)</c> by mistake cannot leak the raw value through the production logger pipeline.
/// </summary>
public sealed partial class SensitiveDataRedactingLoggerFactory(ILoggerFactory inner) : ILoggerFactory
{
    private readonly ConcurrentDictionary<string, ILogger> _loggers = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        inner.AddProvider(provider);
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new RedactingLogger(inner.CreateLogger(name)));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _loggers.Clear();
        inner.Dispose();
    }

    /// <summary>
    /// Apply the redaction policy. Returns the redacted form of <paramref name="input"/> with any <c>Question</c>, <c>AnswerText</c>, or <c>Content</c> assignment replaced with <c>[REDACTED]</c>.
    /// </summary>
    public static string Redact(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var step1 = QuestionField().Replace(input, "Question=\"[REDACTED]\"");
        var step2 = AnswerField().Replace(step1, "AnswerText=\"[REDACTED]\"");
        var step3 = ContentField().Replace(step2, "Content=\"[REDACTED]\"");
        return step3;
    }

    /// <summary>
    /// Replaces the previously registered <see cref="ILoggerFactory"/> in <paramref name="services"/> with a redaction-decorated wrapper. Idempotent if already applied.
    /// </summary>
    public static void RegisterDecorator(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var existing = services.LastOrDefault(d => d.ServiceType == typeof(ILoggerFactory));
        if (existing is null)
        {
            return;
        }

        services.Remove(existing);

        services.Add(ServiceDescriptor.Describe(
            typeof(ILoggerFactory),
            sp =>
            {
                ILoggerFactory innerFactory;
                if (existing.ImplementationFactory is not null)
                {
                    innerFactory = (ILoggerFactory)existing.ImplementationFactory(sp);
                }
                else if (existing.ImplementationInstance is ILoggerFactory instance)
                {
                    innerFactory = instance;
                }
                else if (existing.ImplementationType is not null)
                {
                    innerFactory = (ILoggerFactory)ActivatorUtilities.CreateInstance(sp, existing.ImplementationType);
                }
                else
                {
                    throw new InvalidOperationException("Unable to resolve the inner ILoggerFactory for the redaction decorator.");
                }

                return new SensitiveDataRedactingLoggerFactory(innerFactory);
            },
            existing.Lifetime));
    }

    [GeneratedRegex("Question\\s*=\\s*\"[^\"]*\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QuestionField();

    [GeneratedRegex("AnswerText\\s*=\\s*\"[^\"]*\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AnswerField();

    [GeneratedRegex("Content\\s*=\\s*\"[^\"]*\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ContentField();

    private sealed class RedactingLogger(ILogger inner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            ArgumentNullException.ThrowIfNull(state);
            return inner.BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            if (!inner.IsEnabled(logLevel))
            {
                return;
            }

            inner.Log(
                logLevel,
                eventId,
                state,
                exception,
                (s, e) => Redact(formatter(s, e)));
        }
    }
}
