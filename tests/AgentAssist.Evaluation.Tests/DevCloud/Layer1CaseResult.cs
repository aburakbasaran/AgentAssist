namespace AgentAssist.Evaluation.Tests.DevCloud;

internal sealed record Layer1CaseResult(
    string Id,
    string Category,
    string Question,
    bool ExpectedRefused,
    bool ExpectedEscalation,
    int HttpStatusCode,
    bool ActualRefused,
    int ActualCitationCount,
    bool ActualEscalationRequired,
    string? ActualRefusalReason,
    Layer1OutcomeKind OutcomeKind,
    bool Pass);
