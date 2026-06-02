# Azure integration journal

Operational notes and “dirty truths” discovered while wiring DevCloud and the evaluation harness. No secrets in this file.

## 2026-06-02 — UserSecretsId caused test host to run DevCloud (fail-safe refusal wave)

**Symptom:** `dotnet build` succeeded; `dotnet test` failed in `AgentAssist.Api.IntegrationTests` and `AgentAssist.Evaluation.Tests` (AC-* citation cases refused, `/health/ready` returned 503).

**Cause:** Commit `58797ca` added `UserSecretsId` to `AgentAssist.Api.csproj`. `WebApplicationFactory<Program>` loads user-secrets in Development. Local user-secrets set `AgentAssist:Mode=DevCloud`, which overrides `appsettings.json` (`Mode: Mock`) because user-secrets are registered after JSON. Tests did not set `EVAL_MODE` or force Mock, so the in-process host used real Azure adapters with local credentials/index state.

**Observed behaviour (fail-safe, not fail-open):**

- Azure Search returned no chunks (unavailable, misconfigured test host, or empty matches) → orchestrator refused with *“Bu soruyu yanıtlamak için yeterli kaynak bulunamadı.”* (no-source path), not ungrounded citations.
- Ready health checks failed against Azure dependencies → HTTP 503 on integration tests expecting Mock self-check.

**Fix (FAZ 2):** Shared `AgentAssistWebApplicationFactory` + `EvalHostConfiguration`: when `EVAL_MODE` is **unset**, append in-memory `AgentAssist:Mode=Mock` **after** all other configuration sources so CI and local test runs cannot inherit DevCloud from user-secrets. When `EVAL_MODE=DevCloud`, set mode to DevCloud and overlay only **explicit** `Azure*__` / `AgentAssist__` environment variables so env wins per key and user-secrets remains fallback for unset keys (local `dotnet run` unchanged).

**Article takeaway:** Config precedence in test hosts is part of the safety story; wrong mode did not produce silent grounded hallucinations—it produced systematic refusal.

## 2026-06-02 — FAZ 2: `IOptions` Mock but DI still registered DevCloud adapters

**Symptom:** After adding in-memory `AgentAssist:Mode=Mock` only via `ConfigureAppConfiguration`, `EvalHostConfigurationTests` saw `IOptions.Mode == Mock`, but evaluation still returned no-source refusal for every AC-* case while `evalMode` in JSON was `Mock`.

**Cause:** `Program.cs` chooses `AddMockInfrastructure` vs `AddDevCloudInfrastructure` at startup from configuration. User-secrets could leave `IOptions` binding on Mock (late in-memory overlay) while the **service registration** pass had already read `DevCloud` from an earlier configuration snapshot—so `IKnowledgeSearchService` was still `AzureSearchKnowledgeService` (empty Azure results).

**Fix:** `EvalHostConfiguration` now calls `builder.UseSetting("AgentAssist:Mode", …)` so host configuration wins **before** service registration, plus in-memory overlay for env-first DevCloud keys. Guard test asserts `IKnowledgeSearchService` type name is `MockKnowledgeSearchService` when `EVAL_MODE` is unset.
