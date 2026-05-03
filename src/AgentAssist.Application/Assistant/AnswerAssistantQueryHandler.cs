using System.Globalization;
using System.Text;

using AgentAssist.Application.Abstractions;
using AgentAssist.Application.Ai;
using AgentAssist.Application.Auditing;
using AgentAssist.Application.Common;
using AgentAssist.Domain;
using Microsoft.Extensions.AI;

namespace AgentAssist.Application.Assistant;

/// <summary>
/// Handles assistant query answering through risk classification, retrieval, prompt composition, chat generation, and audit.
/// </summary>
public sealed class AnswerAssistantQueryHandler(
    IRiskClassifier riskClassifier,
    IKnowledgeSearchService knowledgeSearchService,
    IPromptProvider promptProvider,
    IChatClient chatClient,
    IAuditEventSink auditEventSink,
    TimeProvider timeProvider)
    : IRequestHandler<AssistantQuery, Result<AssistantAnswer>>
{
    /// <summary>
    /// The stable prompt template identifier for answer generation.
    /// </summary>
    public const string AnswerTemplateId = "assistant.answer.v1";

    private const string RefusalReason = "Bu soruyu yanıtlamak için yeterli kaynak bulunamadı.";
    private const string QuestionPlaceholder = "{{question}}";
    private const string RetrievedChunksPlaceholder = "{{retrievedChunks}}";

    /// <inheritdoc />
    public async ValueTask<Result<AssistantAnswer>> HandleAsync(AssistantQuery request, CancellationToken ct)
    {
        var risk = await riskClassifier.ClassifyAsync(request, ct).ConfigureAwait(false);
        AssistantAnswer answer;

        var chunks = await knowledgeSearchService.SearchAsync(request, risk, ct).ConfigureAwait(false);
        if (chunks.Count is 0)
        {
            answer = AssistantAnswer.RefusedAnswer(RefusalReason, risk);
            await WriteAuditAsync(request, answer, ct).ConfigureAwait(false);

            return Result<AssistantAnswer>.Success(answer);
        }

        var template = await promptProvider.GetAsync(AnswerTemplateId, ct).ConfigureAwait(false);
        var messages = BuildMessages(template, request, chunks);
        var chatResponse = await chatClient.GetResponseAsync(messages, cancellationToken: ct).ConfigureAwait(false);

        answer = new AssistantAnswer
        {
            AnswerText = chatResponse.Text,
            Citations = chunks.Select(chunk => chunk.ToCitation()).ToArray(),
            ConfidenceLevel = ConfidenceLevel.High,
            RiskClass = risk.RiskClass,
            EscalationRequired = risk.EscalationRequired,
            Refused = false
        };

        await WriteAuditAsync(request, answer, ct).ConfigureAwait(false);

        return Result<AssistantAnswer>.Success(answer);
    }

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
                .Append("] ")
                .Append(chunk.Title)
                .Append(" (")
                .Append(chunk.DocumentId)
                .Append('/')
                .Append(chunk.ChunkId)
                .AppendLine(")")
                .AppendLine(chunk.Content);
        }

        return builder.ToString();
    }

    private ValueTask WriteAuditAsync(AssistantQuery request, AssistantAnswer answer, CancellationToken ct)
    {
        var auditEvent = new AuditEvent
        {
            Timestamp = timeProvider.GetUtcNow(),
            Question = request.Question,
            UserId = request.UserId,
            RiskClass = answer.RiskClass,
            Refused = answer.Refused,
            CitationCount = answer.Citations.Count
        };

        return auditEventSink.WriteAsync(auditEvent, ct);
    }
}
