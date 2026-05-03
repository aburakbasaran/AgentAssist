using AgentAssist.Domain;

namespace AgentAssist.Domain.UnitTests;

public sealed class RiskAssessmentTests
{
    [Fact]
    public void RiskAssessment_HighRisk_TriggersEscalation()
    {
        var assessment = new RiskAssessment
        {
            RiskClass = RiskClass.High,
            Reason = "risk"
        };

        assessment.EscalationRequired.Should().BeTrue();
    }
}
