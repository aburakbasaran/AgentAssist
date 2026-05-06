using AgentAssist.Application.Ai;

namespace AgentAssist.Application.UnitTests;

public sealed class ChatResponseParserTests
{
    [Fact]
    public void ChatResponseParser_ValidJson_ParsesEnvelope()
    {
        const string json = """{"answerText":"hi","citations":["CHK-1"],"confidence":"High","refused":false,"refusalReason":null}""";

        var ok = ChatResponseParser.TryParse(json, out var envelope);

        ok.Should().BeTrue();
        envelope.Should().NotBeNull();
        envelope!.AnswerText.Should().Be("hi");
        envelope.Citations.Should().ContainSingle().Which.Should().Be("CHK-1");
        envelope.Refused.Should().BeFalse();
    }

    [Fact]
    public void ChatResponseParser_RejectsUnmappedJsonMembers()
    {
        const string json = """{"answerText":"hi","citations":["CHK-1"],"confidence":"High","refused":false,"refusalReason":null,"chainOfThought":"leak"}""";

        var ok = ChatResponseParser.TryParse(json, out var envelope);

        ok.Should().BeFalse();
        envelope.Should().BeNull();
    }

    [Fact]
    public void ChatResponseParser_StripsMarkdownCodeFence()
    {
        const string json = """
            ```json
            {"answerText":"hi","citations":["CHK-1"],"confidence":"High","refused":false,"refusalReason":null}
            ```
            """;

        var ok = ChatResponseParser.TryParse(json, out var envelope);

        ok.Should().BeTrue();
        envelope!.AnswerText.Should().Be("hi");
    }

    [Fact]
    public void ChatResponseParser_NotJson_ReturnsFalse()
    {
        var ok = ChatResponseParser.TryParse("plain text", out var envelope);

        ok.Should().BeFalse();
        envelope.Should().BeNull();
    }

    [Fact]
    public void ChatResponseParser_NullOrWhitespace_ReturnsFalse()
    {
        ChatResponseParser.TryParse(null, out _).Should().BeFalse();
        ChatResponseParser.TryParse(string.Empty, out _).Should().BeFalse();
        ChatResponseParser.TryParse("   ", out _).Should().BeFalse();
    }

    [Fact]
    public void ChatResponseParser_MalformedJson_ReturnsFalse()
    {
        const string json = """{"answerText":"hi", "citations":[}""";

        var ok = ChatResponseParser.TryParse(json, out var envelope);

        ok.Should().BeFalse();
        envelope.Should().BeNull();
    }
}
