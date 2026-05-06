using AgentAssist.Domain;
using AgentAssist.Domain.Exceptions;

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

    [Fact]
    public void AssistantAnswer_GroundedFactory_WithCitation_ReturnsNonRefusedAnswer()
    {
        var risk = new RiskAssessment { RiskClass = RiskClass.Low, Reason = "ok" };
        var citation = new Citation { DocumentId = "DOC", ChunkId = "CHK", Title = "Title" };

        var answer = AssistantAnswer.Grounded("text", [citation], ConfidenceLevel.High, risk);

        answer.Refused.Should().BeFalse();
        answer.Citations.Should().ContainSingle().Which.Should().Be(citation);
        answer.AnswerText.Should().Be("text");
        answer.RiskClass.Should().Be(RiskClass.Low);
        answer.EscalationRequired.Should().BeFalse();
    }

    [Fact]
    public void AssistantAnswer_GroundedFactory_WithEmptyCitations_ThrowsUngroundedAnswerException()
    {
        var risk = new RiskAssessment { RiskClass = RiskClass.Low, Reason = "ok" };

        var act = () => AssistantAnswer.Grounded("text", [], ConfidenceLevel.High, risk);

        act.Should().Throw<UngroundedAnswerException>();
    }

    [Fact]
    public void AssistantAnswer_NonRefused_RequiresAtLeastOneCitation()
    {
        var risk = new RiskAssessment { RiskClass = RiskClass.Low, Reason = "ok" };
        var ungrounded = new AssistantAnswer
        {
            AnswerText = "x",
            Citations = [],
            ConfidenceLevel = ConfidenceLevel.High,
            RiskClass = risk.RiskClass,
            EscalationRequired = false,
            Refused = false
        };

        var act = ungrounded.EnsureCitationInvariant;

        act.Should().Throw<UngroundedAnswerException>();
    }

    [Fact]
    public void AssistantAnswer_Refused_PassesEnsureCitationInvariant()
    {
        var risk = new RiskAssessment { RiskClass = RiskClass.Low, Reason = "ok" };

        var refused = AssistantAnswer.RefusedAnswer("no source", risk);
        var act = refused.EnsureCitationInvariant;

        act.Should().NotThrow();
    }
}
