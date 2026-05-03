using AgentAssist.Application.Abstractions;
using AgentAssist.Application.Ai;
using AgentAssist.Application.Assistant;
using AgentAssist.Application.Auditing;
using AgentAssist.Domain;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Time.Testing;

namespace AgentAssist.Application.UnitTests;

public sealed class AnswerAssistantQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_NoRetrievedChunks_ReturnsRefusedAnswer()
    {
        var services = CreateServices();
        services.Search.SearchAsync(Arg.Any<AssistantQuery>(), Arg.Any<RiskAssessment>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<RetrievedChunk>>([]));
        var handler = CreateHandler(services);

        var result = await handler.HandleAsync(CreateQuery("unknown"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        var answer = result.Value ?? throw new InvalidOperationException("Expected an answer.");
        answer.Refused.Should().BeTrue();
        answer.Citations.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_HighRiskQuery_SetsEscalationRequired()
    {
        var services = CreateServices(RiskClass.High);
        services.Search.SearchAsync(Arg.Any<AssistantQuery>(), Arg.Any<RiskAssessment>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<RetrievedChunk>>([CreateChunk()]));
        var handler = CreateHandler(services);

        var result = await handler.HandleAsync(CreateQuery("ilaç dozu"), CancellationToken.None);

        result.Value.Should().NotBeNull();
        var answer = result.Value ?? throw new InvalidOperationException("Expected an answer.");
        answer.EscalationRequired.Should().BeTrue();
        answer.RiskClass.Should().Be(RiskClass.High);
    }

    [Fact]
    public async Task HandleAsync_ValidQuery_CallsRiskClassifyBeforeSearch()
    {
        var services = CreateServices();
        services.Search.SearchAsync(Arg.Any<AssistantQuery>(), Arg.Any<RiskAssessment>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<RetrievedChunk>>([CreateChunk()]));
        var handler = CreateHandler(services);

        await handler.HandleAsync(CreateQuery("MR hazırlık"), CancellationToken.None);

        Received.InOrder(() =>
        {
            services.RiskClassifier.ClassifyAsync(Arg.Any<AssistantQuery>(), Arg.Any<CancellationToken>());
            services.Search.SearchAsync(Arg.Any<AssistantQuery>(), Arg.Any<RiskAssessment>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task HandleAsync_ValidQuery_CallsSearchBeforeChatClient()
    {
        var services = CreateServices();
        services.Search.SearchAsync(Arg.Any<AssistantQuery>(), Arg.Any<RiskAssessment>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<RetrievedChunk>>([CreateChunk()]));
        var handler = CreateHandler(services);

        await handler.HandleAsync(CreateQuery("MR hazırlık"), CancellationToken.None);

        Received.InOrder(() =>
        {
            services.Search.SearchAsync(Arg.Any<AssistantQuery>(), Arg.Any<RiskAssessment>(), Arg.Any<CancellationToken>());
            services.ChatClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task HandleAsync_ChatClientReturnsAnswer_WritesAuditEvent()
    {
        var services = CreateServices();
        services.Search.SearchAsync(Arg.Any<AssistantQuery>(), Arg.Any<RiskAssessment>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<RetrievedChunk>>([CreateChunk()]));
        var handler = CreateHandler(services);

        await handler.HandleAsync(CreateQuery("MR hazırlık"), CancellationToken.None);

        await services.AuditSink.Received(1).WriteAsync(
            Arg.Is<AuditEvent>(auditEvent => !auditEvent.Refused && auditEvent.CitationCount == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SearchReturnsNoChunks_WritesAuditEvent()
    {
        var services = CreateServices();
        services.Search.SearchAsync(Arg.Any<AssistantQuery>(), Arg.Any<RiskAssessment>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<RetrievedChunk>>([]));
        var handler = CreateHandler(services);

        await handler.HandleAsync(CreateQuery("unknown"), CancellationToken.None);

        await services.AuditSink.Received(1).WriteAsync(
            Arg.Is<AuditEvent>(auditEvent => auditEvent.Refused && auditEvent.CitationCount == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_CancellationRequested_PropagatesCancellation()
    {
        var services = CreateServices();
        services.RiskClassifier.ClassifyAsync(Arg.Any<AssistantQuery>(), Arg.Any<CancellationToken>())
            .Returns<ValueTask<RiskAssessment>>(_ => throw new OperationCanceledException());
        var handler = CreateHandler(services);

        var act = async () => await handler.HandleAsync(CreateQuery("MR hazırlık"), CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Handler_ConstructsChatMessages_FromTemplate()
    {
        var services = CreateServices();
        var chunk = CreateChunk();
        ChatMessage[] capturedMessages = [];
        services.Search.SearchAsync(Arg.Any<AssistantQuery>(), Arg.Any<RiskAssessment>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<RetrievedChunk>>([chunk]));
        services.ChatClient.GetResponseAsync(
                Arg.Do<IEnumerable<ChatMessage>>(messages => capturedMessages = messages.ToArray()),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "answer"))));
        var handler = CreateHandler(services);

        await handler.HandleAsync(CreateQuery("MR hazırlık"), CancellationToken.None);

        capturedMessages.Should().HaveCount(2);
        capturedMessages[0].Role.Should().Be(ChatRole.System);
        capturedMessages[0].Text.Should().Contain("system");
        capturedMessages[1].Role.Should().Be(ChatRole.User);
        capturedMessages[1].Text.Should().Contain("MR hazırlık");
        capturedMessages[1].Text.Should().Contain(chunk.Content);
    }

    [Fact]
    public async Task HandleAsync_ChatClientReturnsAnswer_WritesAuditTimestampFromTimeProvider()
    {
        var timestamp = new DateTimeOffset(2026, 5, 3, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(timestamp);
        var services = CreateServices(timeProvider: timeProvider);
        services.Search.SearchAsync(Arg.Any<AssistantQuery>(), Arg.Any<RiskAssessment>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<RetrievedChunk>>([CreateChunk()]));
        var handler = CreateHandler(services);

        await handler.HandleAsync(CreateQuery("MR hazırlık"), CancellationToken.None);

        await services.AuditSink.Received(1).WriteAsync(
            Arg.Is<AuditEvent>(auditEvent => auditEvent.Timestamp == timestamp),
            Arg.Any<CancellationToken>());
    }

    private static TestServices CreateServices(RiskClass riskClass = RiskClass.Low, TimeProvider? timeProvider = null)
    {
        var riskAssessment = new RiskAssessment
        {
            RiskClass = riskClass,
            Reason = "risk"
        };
        var riskClassifier = Substitute.For<IRiskClassifier>();
        riskClassifier.ClassifyAsync(Arg.Any<AssistantQuery>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(riskAssessment));
        var search = Substitute.For<IKnowledgeSearchService>();
        var promptProvider = Substitute.For<IPromptProvider>();
        promptProvider.GetAsync(AnswerAssistantQueryHandler.AnswerTemplateId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new PromptTemplate(
                AnswerAssistantQueryHandler.AnswerTemplateId,
                "system",
                "Question: {{question}}\nChunks: {{retrievedChunks}}")));
        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "answer"))));
        var auditSink = Substitute.For<IAuditEventSink>();
        auditSink.WriteAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        return new TestServices(
            riskClassifier,
            search,
            promptProvider,
            chatClient,
            auditSink,
            timeProvider ?? TimeProvider.System);
    }

    private static AnswerAssistantQueryHandler CreateHandler(TestServices services) => new(
        services.RiskClassifier,
        services.Search,
        services.PromptProvider,
        services.ChatClient,
        services.AuditSink,
        services.TimeProvider);

    private static AssistantQuery CreateQuery(string question) => new()
    {
        Question = question,
        UserId = "user-1",
        Roles = ["agent"]
    };

    private static RetrievedChunk CreateChunk() => new()
    {
        DocumentId = "DOC-001",
        ChunkId = "CHK-001",
        Title = "MR hazırlık",
        Content = "MR hazırlık içeriği",
        AllowedRoles = ["agent"],
        DocumentType = DocumentType.Guidance,
        RiskLevel = RiskClass.Low,
        Score = 0.9D
    };

    private sealed record TestServices(
        IRiskClassifier RiskClassifier,
        IKnowledgeSearchService Search,
        IPromptProvider PromptProvider,
        IChatClient ChatClient,
        IAuditEventSink AuditSink,
        TimeProvider TimeProvider);
}
