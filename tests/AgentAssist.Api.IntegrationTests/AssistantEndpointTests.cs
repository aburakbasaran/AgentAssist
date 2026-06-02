using AgentAssist.Domain;
using AgentAssist.Testing;

namespace AgentAssist.Api.IntegrationTests;

public sealed class AssistantEndpointTests : IClassFixture<AgentAssistWebApplicationFactory>
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private const string AgentUserHeader = "X-Agent-User";
    private const string AgentRolesHeader = "X-Agent-Roles";
    private const string AgentLocationHeader = "X-Agent-Location";

    private readonly AgentAssistWebApplicationFactory _factory;

    public AssistantEndpointTests(AgentAssistWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAgentClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(AgentUserHeader, "pilot-user");
        client.DefaultRequestHeaders.Add(AgentRolesHeader, "agent");
        client.DefaultRequestHeaders.Add(AgentLocationHeader, "branch-a");
        return client;
    }

    [Fact]
    public async Task PostQuery_ValidQuestion_ReturnsOkAssistantAnswer()
    {
        var client = CreateAgentClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/v1/assistant/query", new
        {
            question = "MR randevu hazırlık bilgisi nedir?"
        }, ct);
        var answer = await response.Content.ReadFromJsonAsync<AssistantAnswer>(ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        answer.Should().NotBeNull();
        var assistantAnswer = answer ?? throw new InvalidOperationException("Expected an answer.");
        assistantAnswer.Refused.Should().BeFalse();
        assistantAnswer.Citations.Should().NotBeEmpty();
        assistantAnswer.EscalationRequired.Should().BeFalse();
    }

    [Fact]
    public async Task PostQuery_EmptyQuestion_ReturnsValidationProblem()
    {
        var client = CreateAgentClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/v1/assistant/query", new
        {
            question = string.Empty
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostQuery_QuestionLongerThanLimit_ReturnsValidationProblem()
    {
        var client = CreateAgentClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/v1/assistant/query", new
        {
            question = new string('a', 2001)
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetLiveHealth_Always_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/health/live", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetReadyHealth_Always_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/health/ready", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostQuery_HighRiskKeyword_ReturnsOkWithEscalationRequired()
    {
        var client = CreateAgentClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/v1/assistant/query", new
        {
            question = "ilaç dozu hakkında yönergeler neler?"
        }, ct);
        var answer = await response.Content.ReadFromJsonAsync<AssistantAnswer>(ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        answer.Should().NotBeNull();
        var assistantAnswer = answer ?? throw new InvalidOperationException("Expected an answer.");
        assistantAnswer.EscalationRequired.Should().BeTrue();
    }

    [Fact]
    public async Task PostQuery_UnknownKeyword_ReturnsOkRefusedAnswer()
    {
        var client = CreateAgentClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/v1/assistant/query", new
        {
            question = "tamamen alakasız bilinmeyen konu"
        }, ct);
        var answer = await response.Content.ReadFromJsonAsync<AssistantAnswer>(ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        answer.Should().NotBeNull();
        var assistantAnswer = answer ?? throw new InvalidOperationException("Expected an answer.");
        assistantAnswer.Refused.Should().BeTrue();
        assistantAnswer.Citations.Should().BeEmpty();
    }

    [Fact]
    public async Task PostQuery_WithCorrelationIdHeader_EchoesCorrelationIdHeader()
    {
        var client = CreateAgentClient();
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/assistant/query");
        request.Headers.Add(CorrelationIdHeader, "test-correlation-id");
        request.Content = JsonContent.Create(new
        {
            question = "MR randevu hazırlık bilgisi nedir?"
        });

        var response = await client.SendAsync(request, ct);

        response.Headers.GetValues(CorrelationIdHeader).Should().Contain("test-correlation-id");
    }

    [Fact]
    public async Task PostQuery_WithoutCorrelationIdHeader_GeneratesCorrelationIdHeader()
    {
        var client = CreateAgentClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/v1/assistant/query", new
        {
            question = "MR randevu hazırlık bilgisi nedir?"
        }, ct);

        response.Headers.Contains(CorrelationIdHeader).Should().BeTrue();
    }

    [Fact]
    public async Task PostQuery_RequestBodyRolesField_ReturnsBadRequestWithAuthenticationContextMessage()
    {
        var client = CreateAgentClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/v1/assistant/query", new
        {
            question = "MR randevu hazırlık bilgisi nedir?",
            roles = new[] { "spoofed-superadmin" }
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("X-Agent-Roles");
        body.Should().Contain("not allowed");
    }

    [Fact]
    public async Task PostQuery_RequestBodyUserIdField_ReturnsBadRequestWithAuthenticationContextMessage()
    {
        var client = CreateAgentClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/v1/assistant/query", new
        {
            question = "MR randevu hazırlık bilgisi nedir?",
            userId = "ghost-user"
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("X-Agent-User");
    }

    [Fact]
    public async Task PostQuery_UnknownField_ReturnsBadRequest()
    {
        var client = CreateAgentClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/v1/assistant/query", new
        {
            question = "MR randevu hazırlık bilgisi nedir?",
            isAdmin = true
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostQuery_NoAgentHeaders_ReturnsRefusedAnswer()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/v1/assistant/query", new
        {
            question = "MR randevu hazırlık bilgisi nedir?"
        }, ct);
        var answer = await response.Content.ReadFromJsonAsync<AssistantAnswer>(ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        answer.Should().NotBeNull();
        answer!.Refused.Should().BeTrue();
    }

    [Fact]
    public async Task PostFeedback_ValidPayload_ReturnsAccepted()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/v1/assistant/feedback", new
        {
            correlationId = "test-corr-1",
            helpful = true,
            reason = "Çok yardımcı oldu"
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task PostFeedback_MissingCorrelationId_ReturnsValidationProblem()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/v1/assistant/feedback", new
        {
            correlationId = string.Empty,
            helpful = false
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
