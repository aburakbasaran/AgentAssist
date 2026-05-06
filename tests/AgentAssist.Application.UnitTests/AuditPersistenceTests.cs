using AgentAssist.Application.Assistant;
using AgentAssist.Application.Auditing;

namespace AgentAssist.Application.UnitTests;

public sealed class AuditPersistenceTests
{
    [Fact]
    public void Audit_Question_StoredAsHashAndPreview_NotRaw()
    {
        const string question = "MR randevu hazırlık bilgisi nedir?";

        var hash = AnswerAssistantQueryHandler.ComputeQuestionHash(question);
        var preview = AnswerAssistantQueryHandler.BuildQuestionPreview(question);

        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().HaveLength(64);
        preview.Should().Be(question);
    }

    [Fact]
    public void Audit_HashIsDeterministic()
    {
        const string question = "Stable question";

        var hash1 = AnswerAssistantQueryHandler.ComputeQuestionHash(question);
        var hash2 = AnswerAssistantQueryHandler.ComputeQuestionHash(question);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void Audit_PreviewTruncatesLongQuestion()
    {
        var longQuestion = new string('a', 200);

        var preview = AnswerAssistantQueryHandler.BuildQuestionPreview(longQuestion);

        preview.Length.Should().BeLessThanOrEqualTo(81);
    }

    [Theory]
    [InlineData("Hasta no 12345678901 sorusu", "[redacted-number]")]
    [InlineData("Card 1234567812345678 problem", "[redacted-number]")]
    public void Audit_RedactsSensitiveNumberPatterns_FromQuestionPreview(string question, string mustContain)
    {
        var preview = AnswerAssistantQueryHandler.BuildQuestionPreview(question);

        preview.Should().Contain(mustContain);
        preview.Should().NotContain("12345678901");
        preview.Should().NotContain("1234567812345678");
    }

    [Fact]
    public void Audit_PreviewRedactsBeforeTruncation()
    {
        var question = "Hasta " + new string('a', 75) + " 12345678901";

        var preview = AnswerAssistantQueryHandler.BuildQuestionPreview(question);

        preview.Should().NotContain("12345678901");
    }

    [Fact]
    public void SensitiveNumberRedactor_NoMatch_ReturnsOriginal()
    {
        const string text = "Sadece kelimeler içeriyor";

        var actual = SensitiveNumberRedactor.Redact(text);

        actual.Should().Be(text);
    }

    [Fact]
    public void SensitiveNumberRedactor_NullSafe_ReturnsEmpty()
    {
        SensitiveNumberRedactor.Redact(string.Empty).Should().Be(string.Empty);
    }
}
