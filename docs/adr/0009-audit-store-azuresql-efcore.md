# ADR-0009: Audit Store on Azure SQL via EF Core 10

The production pilot persists assistant audit events and feedback to Azure SQL through EF Core 10. The audit store choice prefers Azure SQL over Cosmos DB because:

- Internal pilot teams are more comfortable with relational tooling and ad-hoc SQL queries.
- Tabular schema (CorrelationId, Mode, RetrievalCount, CitationCount, ConfidenceLevel, EscalationRequired, RefusalReason, LatencyMs, QuestionHash, QuestionPreview, CreatedAt) maps cleanly to a relational table and is small in volume for a pilot.
- A single, well-understood migration path via `dotnet ef migrations` keeps onboarding simple.

The audit policy is **best-effort**: if the audit write fails (SQL transient failure, throttling, etc.) the handler logs a warning and still returns the user response. This deliberately favours availability over a strict "audit-or-fail" stance because no customer obligation requires audit guarantees in this pilot. A migration to "audit-or-fail" — or to an outbox pattern — is a follow-up gap recorded in the quality gate.

Health-check probes never call chat completion (cost/quota concern). The `AzureOpenAIHealthCheck` uses lightweight data-plane reachability calls, cached with a configurable TTL (default 60 seconds). A deeper end-to-end probe is documented as an optional `/health/deep` endpoint for the future, off by default.

EF Core 10 is referenced **only from `AgentAssist.Infrastructure`**; `AgentAssist.Application` and `AgentAssist.Domain` continue to be EF Core-free per the boundary tests in `AgentAssist.Architecture.Tests`. Audit/feedback entities and `IEntityTypeConfiguration<T>` configurations are infrastructure-internal types; the Application layer talks to `IAuditEventSink` and `IFeedbackSink` only.

This ADR is finalised in Slice 4; implementation lives in `src/AgentAssist.Infrastructure/Persistence/`.
