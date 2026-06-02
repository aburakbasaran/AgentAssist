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

## 2026-06-02 — FAZ 3: DevCloud eval requires `AZURE_TENANT_ID` + semantic-only overlay

**Symptom:** `EVAL_MODE=DevCloud` with user-secrets loaded still yielded 401 on Search and orchestrator no-source for all cases.

**Cause:** `DefaultAzureCredential` in the test process picked the wrong tenant without `AZURE_TENANT_ID`. Separately, `MinChunkScore=0.7` filtered semantic reranker scores so only some queries (e.g. AC-001) returned chunks.

**Fix:** Document/set `AZURE_TENANT_ID` (e.g. from `az account show --query tenantId -o tsv`) for DevCloud eval runs. `EvalHostConfiguration` now: explicit `AddUserSecrets`, `EVAL_SEMANTIC_ONLY` default (empty vector/embedding), env overlay for `AgentAssist__MinChunkScore`. Katman 1 classifies orchestrator no-source on answerable cases as `RetrievalIndexGap` when AC-001 connectivity probe passed (not `InvalidInfrastructure`).

## 2026-06-02 — FAZ 3.5: Golden pilot index + MinChunkScore 0.7

**Index:** Nine chunks uploaded via `GoldenPilotIndexUploadTests` from `test/data/azure-search/golden-pilot-knowledge.json`. Azure document keys use `DOC-xxx_CHK-yyy` (colons in `id` are rejected by Search). Legacy single-doc key `acme-mr-prep-001` deleted to avoid duplicate CHK-001 noise.

**Threshold:** `eval/results/minchunk-threshold-analysis.json` — all six AC-* cases have expected chunk top-1 at **0.7**; **0.5** no longer required.

**Katman 1:** 18/20 pass at default `MinChunkScore=0.7` (`layer1-devcloud-latest.json`). HR-001: CHK-006 semantic score **0.696** (just below 0.7) → orchestrator no-source. HR-002: real model returns `refused:true` in envelope with CHK-006 in JSON; handler maps to self-refusal without surfacing citations (Mock always returns `refused:false` + citation when chunks exist).

**AD-002:** Agent never retrieves SECRET-CHK (supervisor-only OData). Orchestrator no-source before LLM — first defense layer. `invalid_citation` E2E not observed (by design); whitelist proof is `CitationValidatorTests` + `Handler_ModelReturnsUnknownCitation_ReturnsRefusal`.
