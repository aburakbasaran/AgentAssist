using AgentAssist.Domain;

namespace AgentAssist.Domain.UnitTests;

public sealed class AssistantAnswerTests
{
    [Fact]
    public void AssistantAnswer_RefusedFactory_ReturnsStructuredRefusal()
    {
        var risk = new RiskAssessment
        {
            RiskClass = RiskClass.High,
            Reason = "risk"
        };

        var answer = AssistantAnswer.RefusedAnswer("No source", risk);

        answer.Refused.Should().BeTrue();
        answer.AnswerText.Should().Be("No source");
        answer.RefusalReason.Should().Be("No source");
        answer.RiskClass.Should().Be(RiskClass.High);
        answer.EscalationRequired.Should().BeTrue();
    }

    [Fact]
    public void AssistantAnswer_RefusedFactory_HasEmptyCitations()
    {
        var risk = new RiskAssessment
        {
            RiskClass = RiskClass.Low,
            Reason = "risk"
        };

        var answer = AssistantAnswer.RefusedAnswer("No source", risk);

        answer.Citations.Should().BeEmpty();
    }
}
