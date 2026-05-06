using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using AgentAssist.Application.Abstractions;
using AgentAssist.Application.Ai;
using AgentAssist.Application.Auditing;
using AgentAssist.Application.Common;
using AgentAssist.Application.Configuration;
using AgentAssist.Domain;
using AgentAssist.Domain.Exceptions;
using FluentValidation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentAssist.Application.Assistant;

/// <summary>
/// Handles assistant query answering through validation, risk classification, retrieval, prompt composition, structured citation validation, and best-effort audit. User identity (<see cref="AssistantQuery.UserId"/>, <see cref="AssistantQuery.Roles"/>, <see cref="AssistantQuery.Location"/>) is sourced from <see cref="IUserContextProvider"/>; any matching values supplied on the request body are intentionally ignored (see ADR-0010).
/// </summary>
public sealed class AnswerAssistantQueryHandler(
    IValidator<AssistantQuery> queryValidator,
    IUserContextProvider userContextProvider,
    IRiskClassifier riskClassifier,
    IKnowledgeSearchService knowledgeSearchService,
    IPromptProvider promptProvider,
    IChatClient chatClient,
    IAuditEventSink auditEventSink,
    IAgentAssistMetrics metrics,
    IOptions<AgentAssistOptions> agentOptions,
    ILogger<AnswerAssistantQueryHandler> logger,
    TimeProvider timeProvider)
    : IRequestHandler<AssistantQuery, Result<AssistantAnswer>>
{
    /// <summary>
    /// The stable prompt template identifier for answer generation.
    /// </summary>
    public const string AnswerTemplateId = "assistant.answer.v1";

    private const string NoSourceRefusalReason = "Bu soruyu yanıtlamak için yeterli kaynak bulunamadı.";
    private const string MalformedResponseReason = "model_returned_malformed_response";
    private const string InvalidCitationReason = "model_returned_invalid_citation";
    private const string QuestionPlaceholder = "{{question}}";
    private const string RetrievedChunksPlaceholder = "{{retrievedChunks}}";

    /// <inheritdoc />
    public async ValueTask<Result<AssistantAnswer>> HandleAsync(AssistantQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        // reason: pilot user context is the only authoritative source for UserId/Roles/Location; values on the request body are ignored to keep retrieval filters honest (ADR-0010).
        request = request with
        {
            UserId = userContextProvider.UserId,
            Roles = userContextProvider.Roles,
            Location = userContextProvider.Location
        };

        var validation = await queryValidator.ValidateAsync(request, ct).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            throw new InvalidAssistantQueryException(validation.ToString("; "));
        }

        var startTimestamp = Stopwatch.GetTimestamp();
        var retrievalCount = 0;
        var mode = agentOptions.Value.Mode;
        metrics.RecordProviderMode(mode);

        var risk = await riskClassifier.ClassifyAsync(request, ct).ConfigureAwait(false);
        metrics.RecordRiskClass(risk.RiskClass, mode);
        AssistantAnswer answer;

        var chunks = await knowledgeSearchService.SearchAsync(request, risk, ct).ConfigureAwait(false);
        retrievalCount = chunks.Count;
        metrics.RecordRetrievalCount(retrievalCount, mode);
        if (chunks.Count is 0)
        {
            answer = AssistantAnswer.RefusedAnswer(NoSourceRefusalReason, risk);
            EmitAnswerMetrics(answer, mode);
            await WriteAuditAsync(request, answer, retrievalCount, startTimestamp, ct).ConfigureAwait(false);

            return Result<AssistantAnswer>.Success(answer);
        }

        var template = await promptProvider.GetAsync(AnswerTemplateId, ct).ConfigureAwait(false);
        var messages = BuildMessages(template, request, chunks);
        var chatResponse = await chatClient.GetResponseAsync(messages, cancellationToken: ct).ConfigureAwait(false);
        RecordTokenUsageIfPresent(chatResponse, mode);

        if (!ChatResponseParser.TryParse(chatResponse.Text, out var envelope) || envelope is null)
        {
            answer = AssistantAnswer.RefusedAnswer(MalformedResponseReason, risk);
            EmitAnswerMetrics(answer, mode);
            await WriteAuditAsync(request, answer, retrievalCount, startTimestamp, ct).ConfigureAwait(false);
            return Result<AssistantAnswer>.Success(answer);
        }

        if (envelope.Refused)
        {
            answer = AssistantAnswer.RefusedAnswer(envelope.RefusalReason ?? envelope.AnswerText, risk);
            EmitAnswerMetrics(answer, mode);
            await WriteAuditAsync(request, answer, retrievalCount, startTimestamp, ct).ConfigureAwait(false);
            return Result<AssistantAnswer>.Success(answer);
        }

        var citationValidation = CitationValidator.Validate(envelope.Citations, chunks);
        if (citationValidation.Outcome is not CitationValidationOutcome.Valid)
        {
            answer = AssistantAnswer.RefusedAnswer(InvalidCitationReason, risk);
            EmitAnswerMetrics(answer, mode);
            await WriteAuditAsync(request, answer, retrievalCount, startTimestamp, ct).ConfigureAwait(false);
            return Result<AssistantAnswer>.Success(answer);
        }

        var chunkLookup = chunks.ToDictionary(chunk => chunk.ChunkId, StringComparer.Ordinal);
        var citations = envelope.Citations
            .Select(id => chunkLookup[id].ToCitation())
            .ToArray();

        answer = AssistantAnswer.Grounded(
            envelope.AnswerText,
            citations,
            MapConfidenceLevel(envelope.Confidence),
            risk);
        answer.EnsureCitationInvariant();

        EmitAnswerMetrics(answer, mode);
        await WriteAuditAsync(request, answer, retrievalCount, startTimestamp, ct).ConfigureAwait(false);

        return Result<AssistantAnswer>.Success(answer);
    }

    private void RecordTokenUsageIfPresent(ChatResponse chatResponse, AgentAssistMode mode)
    {
        // reason: Microsoft.Extensions.AI surfaces token counts on ChatResponse.Usage when the upstream provider returns them. The deterministic mock does not populate Usage, so the histograms simply skip the measurement (see ADR-0008).
        var usage = chatResponse.Usage;
        if (usage is null)
        {
            return;
        }

        metrics.RecordTokenUsage(usage.InputTokenCount, usage.OutputTokenCount, mode);
    }

    private void EmitAnswerMetrics(AssistantAnswer answer, AgentAssistMode mode)
    {
        metrics.RecordCitationCount(answer.Citations.Count, mode);
        metrics.RecordConfidence(answer.ConfidenceLevel, mode);
        if (answer.EscalationRequired)
        {
            metrics.RecordEscalation(answer.RiskClass, mode);
        }

        if (answer.Refused)
        {
            metrics.RecordRefusal(answer.RefusalReason ?? "unspecified", mode);
        }
    }

    private static ConfidenceLevel MapConfidenceLevel(string? value) => value switch
    {
        "Low" => ConfidenceLevel.Low,
        "Medium" => ConfidenceLevel.Medium,
        "High" => ConfidenceLevel.High,
        _ => ConfidenceLevel.High
    };

    /// <summary>
    /// Builds chat messages from a prompt template and retrieved chunks.
    /// </summary>
    /// <param name="template">The prompt template.</param>
    /// <param name="query">The assistant query.</param>
    /// <param name="chunks">The retrieved chunks.</param>
    /// <returns>The chat messages.</returns>
    public static IReadOnlyList<ChatMessage> BuildMessages(
        PromptTemplate template,
        AssistantQuery query,
        IReadOnlyList<RetrievedChunk> chunks)
    {
        var retrievedChunks = BuildRetrievedChunksText(chunks);
        var userMessage = template.UserMessageFormat
            .Replace(QuestionPlaceholder, query.Question, StringComparison.Ordinal)
            .Replace(RetrievedChunksPlaceholder, retrievedChunks, StringComparison.Ordinal);

        return
        [
            new ChatMessage(ChatRole.System, template.SystemMessage),
            new ChatMessage(ChatRole.User, userMessage)
        ];
    }

    private static string BuildRetrievedChunksText(IReadOnlyList<RetrievedChunk> chunks)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index];
            _ = builder
                .Append('[')
                .Append((index + 1).ToString(CultureInfo.InvariantCulture))
                .Append("] chunkId=\"")
                .Append(chunk.ChunkId)
                .AppendLine("\"")
                .Append("title: ")
                .AppendLine(chunk.Title)
                .Append("content: ")
                .AppendLine(chunk.Content);
        }

        return builder.ToString();
    }

    private async ValueTask WriteAuditAsync(
        AssistantQuery request,
        AssistantAnswer answer,
        int retrievalCount,
        long startTimestamp,
        CancellationToken ct)
    {
        var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
        var latencyMs = (long)elapsed.TotalMilliseconds;
        var mode = agentOptions.Value.Mode;
        metrics.RecordQueryLatency(latencyMs, mode);

        var auditEvent = new AuditEvent
        {
            Timestamp = timeProvider.GetUtcNow(),
            CorrelationId = ResolveCorrelationId(request),
            Mode = mode,
            UserId = request.UserId,
            QuestionHash = ComputeQuestionHash(request.Question),
            QuestionPreview = BuildQuestionPreview(request.Question),
            RetrievalCount = retrievalCount,
            CitationCount = answer.Citations.Count,
            ConfidenceLevel = answer.ConfidenceLevel,
            RiskClass = answer.RiskClass,
            EscalationRequired = answer.EscalationRequired,
            Refused = answer.Refused,
            RefusalReason = answer.RefusalReason,
            LatencyMs = latencyMs
        };

        try
        {
            await auditEventSink.WriteAsync(auditEvent, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // reason: audit is best-effort per ADR-0009; persistence failure must not break the user response.
            logger.LogWarning(ex, "Audit write failed for correlation {CorrelationId}; continuing.", auditEvent.CorrelationId);
            metrics.RecordAuditWriteFailed(mode);
        }
    }

    private static string ResolveCorrelationId(AssistantQuery request) =>
        string.IsNullOrWhiteSpace(request.CorrelationId)
            ? "unknown"
            : request.CorrelationId;

    /// <summary>
    /// Computes a stable SHA-256 hex hash of the question text. Uppercase hex characters; never reversible.
    /// </summary>
    /// <param name="question">The original question.</param>
    /// <returns>A 64-character hex hash.</returns>
    public static string ComputeQuestionHash(string question)
    {
        ArgumentNullException.ThrowIfNull(question);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(question));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Builds a sanitized preview of the question, redacting common sensitive number patterns and truncating to 80 characters.
    /// </summary>
    /// <param name="question">The original question.</param>
    /// <returns>A sanitized preview suitable for audit storage.</returns>
    public static string BuildQuestionPreview(string question)
    {
        ArgumentNullException.ThrowIfNull(question);
        var redacted = SensitiveNumberRedactor.Redact(question);
        return redacted.Length <= 80
            ? redacted
            : string.Concat(redacted.AsSpan(0, 80), "…");
    }
}
