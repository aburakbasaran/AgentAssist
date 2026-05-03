using AgentAssist.Application.Abstractions;
using AgentAssist.Domain;

namespace AgentAssist.Infrastructure.Mocks;

internal sealed class MockRiskClassifier : IRiskClassifier
{
    private static readonly string[] HighRiskKeywords = ["hasta", "ilaç", "ilac", "doz", "sigorta"];

    public ValueTask<RiskAssessment> ClassifyAsync(AssistantQuery query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var riskClass = ContainsAny(query.Question, HighRiskKeywords)
            ? RiskClass.High
            : RiskClass.Low;

        var reason = riskClass is RiskClass.High
            ? "Query contains regulated-industry risk keywords."
            : "No regulated-industry risk keywords detected.";

        return ValueTask.FromResult(new RiskAssessment
        {
            RiskClass = riskClass,
            Reason = reason
        });
    }

    private static bool ContainsAny(string value, IEnumerable<string> keywords)
    {
        foreach (var keyword in keywords)
        {
            if (value.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
