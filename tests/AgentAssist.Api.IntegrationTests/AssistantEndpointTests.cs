using AgentAssist.Domain;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentAssist.Api.IntegrationTests;

public sealed class AssistantEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    private readonly WebApplicationFactory<Program> _factory;

    public AssistantEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostQuery_ValidQuestion_ReturnsOkAssistantAnswer()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/v1/assistant/query", new
        {
            question = "MR randevu hazırlık bilgisi nedir?",
            roles = new[] { "agent" }
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
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/v1/assistant/query", new
        {
            question = string.Empty,
            roles = new[] { "agent" }
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostQuery_QuestionLongerThanLimit_ReturnsValidationProblem()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/v1/assistant/query", new
        {
            question = new string('a', 2001),
            roles = new[] { "agent" }
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
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/v1/assistant/query", new
        {
            question = "ilaç dozu hakkında yönergeler neler?",
            roles = new[] { "agent" }
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
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/v1/assistant/query", new
        {
            question = "tamamen alakasız bilinmeyen konu",
            roles = new[] { "agent" }
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
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/assistant/query");
        request.Headers.Add(CorrelationIdHeader, "test-correlation-id");
        request.Content = JsonContent.Create(new
        {
            question = "MR randevu hazırlık bilgisi nedir?",
            roles = new[] { "agent" }
        });

        var response = await client.SendAsync(request, ct);

        response.Headers.GetValues(CorrelationIdHeader).Should().Contain("test-correlation-id");
    }

    [Fact]
    public async Task PostQuery_WithoutCorrelationIdHeader_GeneratesCorrelationIdHeader()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/v1/assistant/query", new
        {
            question = "MR randevu hazırlık bilgisi nedir?",
            roles = new[] { "agent" }
        }, ct);

        response.Headers.Contains(CorrelationIdHeader).Should().BeTrue();
    }
}
