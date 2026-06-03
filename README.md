# Agent Assist Enterprise .NET Azure Project

**Status:** Production pilot and two-layer DevCloud evaluation delivered on `master` (see [Evaluation](#evaluation)); mock vertical slice remains available in `Mock` mode. Internal pilot / reference architecture scope — not public production.

Companion repository for a three-part Medium article series (Turkish). The articles link to this repo; this README is the first screen for readers arriving from the series.

**Runtime:** .NET 10 / C# 14 ([ADR-0001](docs/adr/0001-net10-lts-runtime.md); `TargetFramework` is `net10.0` in project files).

Production-grade reference architecture for regulated-industry RAG. Phase A is a mock-first vertical slice: deterministic in-memory knowledge, a mock `IChatClient`, embedded prompt templates, and no Azure SDK calls. The production pilot swaps mocks for Azure AI Search and Azure OpenAI through the same `IKnowledgeSearchService` / `IChatClient` contracts, adds structured citation validation, persistent audit, observability, and a health-checked deployment recipe.

The healthcare-flavored documents in this repository are illustrative. They use placeholder names such as Acme Sağlık Grubu, Şube A/B/C, and Doktor X. No real PHI/PII is included.

## Article series

This repo is the companion for:

1. [Chatbot değil, sistem tasarlamak — güvenli sistemler (1)](https://medium.com/@a.burakbasaran/chatbot-değil-sistem-tasarlamak-güvenli-sistemler-oluşturmak-1-9976a86a2d9d)
2. [“Bilmiyorum” diyebilen yapay zekâ — refusal ve citation sözleşmesini net (2)](https://medium.com/@a.burakbasaran/bilmiyorum-diyebilen-yapay-zekâ-refusal-ve-citation-sözleşmesini-net-ebfc686b9071)
3. [Mock değil, gerçek model — refusal ve citation sözleşmesini Azure’da ölçmek (3)](https://medium.com/@a.burakbasaran/mock-de%C4%9Fil-ger%C3%A7ek-model-refusal-ve-citation-s%C3%B6zle%C5%9Fmesini-azureda-%C3%B6l%C3%A7mek-g%C3%BCvenli-sistemler-1ab4bbef99ce)


## What this is

| Layer | Project | Role |
|---|---|---|
| Domain | `AgentAssist.Domain` | Pure contract types (queries, answers, citations) |
| Application | `AgentAssist.Application` | Orchestrator, contract rules, validators |
| Infrastructure | `AgentAssist.Infrastructure` | Azure and Mock adapters |
| Host | `AgentAssist.Api` | ASP.NET Core Minimal API |

**Refusal + citation contract** (orchestrator: `AnswerAssistantQueryHandler`):

1. **No retrieval** — before the LLM; user-facing reason: `Bu soruyu yanıtlamak için yeterli kaynak bulunamadı.`
2. **Malformed model JSON** — `model_returned_malformed_response`
3. **Model self-refusal** — `envelope.RefusalReason` or `envelope.AnswerText`
4. **Citation not in retrieved whitelist** — `model_returned_invalid_citation`

**Retrieval filter** (Azure AI Search): every query applies `isActive eq true` plus role allow-list (`allowedRoles/any(...)`); raw user role strings never reach OData — only canonical values from `AzureSearchAllowList` (`AzureSearchFilterBuilder`).

Further reading: [`docs/architecture/production-pilot-overview.md`](docs/architecture/production-pilot-overview.md), [`docs/architecture/phase-a-overview.md`](docs/architecture/phase-a-overview.md), and [`docs/adr/`](docs/adr/).

## Evaluation

Two layers separate **contract behavior** (binary) from **answer quality** (scored).

### Layer 1 — contract behavior (binary)

Runs the full golden set against real Azure when `EVAL_MODE=DevCloud`. Checks include: no-source refusal, role filter, inactive filter, adversarial cases, and citation validity.

**Latest committed result:** [`eval/results/layer1-devcloud-latest.json`](eval/results/layer1-devcloud-latest.json) (`evalMode`: `DevCloud`, `semanticOnly`: `true`)

| Metric | Value |
|---|---|
| `totalCases` | 20 |
| `passCount` | 19 |
| `outcomeKind` — `GroundedWithCitations` | 8 |
| `outcomeKind` — `ValidContractRefusal` | 12 |

The single failing case is **HR-002** (`high_risk_escalation`): expected refused + escalation; observed `ValidContractRefusal` with the no-source user message (retrieval did not surface a chunk).

### Layer 2 — answer quality (scored)

`GroundednessEvaluator` + `RelevanceTruthAndCompletenessEvaluator` on a 1–5 scale; **3 runs per case** (`runsPerCase`: 3). Judge uses the **same** chat deployment as the producer (`judgeSameAsProducer`: true — self-grading; treat scores accordingly).

**Latest committed result:** [`eval/results/layer2-quality-latest.json`](eval/results/layer2-quality-latest.json)

| Metric | Value |
|---|---|
| `casesTotal` / `casesCompleted` | 8 / 8 |
| `totalScoredRuns` | 24 |
| Grounded cases | AC-001 … AC-006, HR-001, HR-003 |

**Example finding (AC-004):** groundedness mean **5** across 3 runs; completeness mean **2** (judge: response is accurate but lacks step-by-step detail vs. the question “Şube transfer prosedürü adımları”).

### Golden set and reports

- Golden set: [`eval/golden-set.production-pilot.jsonl`](eval/golden-set.production-pilot.jsonl) — categories: `answerable_with_citation`, `no_source_refusal`, `high_risk_escalation`, `role_restricted`, `inactive_filter`, `adversarial_prompt_injection`
- Raw JSON outputs: [`eval/results/`](eval/results/)
- Detailed write-ups: [`docs/quality-gates/production-pilot-quality-gate.md`](docs/quality-gates/production-pilot-quality-gate.md), [`docs/quality-gates/production-pilot-quality-gate-result.md`](docs/quality-gates/production-pilot-quality-gate-result.md)

### Run evaluation tests

Project: `tests/AgentAssist.Evaluation.Tests`. When `EVAL_MODE` is **unset**, the host forces `Mock` (CI-safe). Set `EVAL_MODE=DevCloud` and configure Azure via `dotnet user-secrets` on `AgentAssist.Api` (see [Configuration](#configuration-mock-vs-devcloud)).

**Mock harness** (no Azure; all golden categories in Mock):

```powershell
dotnet test tests/AgentAssist.Evaluation.Tests --configuration Release
```

**Layer 1 — DevCloud golden set** (writes `eval/results/layer1-devcloud-latest.json`):

```powershell
$env:EVAL_MODE = 'DevCloud'
dotnet test tests/AgentAssist.Evaluation.Tests --configuration Release --filter "FullyQualifiedName~Layer1_DevCloud_RunGoldenSet"
```

**Layer 2 — DevCloud quality metrics** (writes `eval/results/layer2-quality-latest.json`; long-running):

```powershell
$env:EVAL_MODE = 'DevCloud'
dotnet test tests/AgentAssist.Evaluation.Tests --configuration Release --filter "FullyQualifiedName~Layer2_DevCloud_QualityMetrics_WriteResults"
```

One-shot index seeding before Layer 1: test `GoldenPilotIndexUploadTests` (same `EVAL_MODE=DevCloud`). By default, DevCloud eval forces **semantic-only** retrieval (`EVAL_SEMANTIC_ONLY` unset or not `false` clears vector field and embedding deployment in the test host — matches `semanticOnly: true` in Layer 1 results).

### Honest limits (pilot)

- Retrieval and generation hit **real** Azure AI Search + Azure OpenAI in DevCloud eval.
- **Risk classifier** is still `MockRiskClassifier` (keyword-based) in both Mock and DevCloud composition.
- **Retrieval metrics** (precision/recall) are not measured — no retrieval ground-truth labels in the golden set.

## Production Hardening Out of Scope

This reference architecture targets an internal pilot. Endpoint authentication, **rate limiting**, API gateway / WAF concerns (Entra ID, APIM, Private Endpoints / VNet integration, production-mode anonymous fail-fast) are intentionally **out of scope**; ADR-0010 and the Azure setup guide list the hardening steps required before exposing this app outside a trusted network.

## Testing Stack

The test stack uses xUnit v3, NSubstitute, Microsoft `FakeTimeProvider`, and AwesomeAssertions (MIT-licensed FluentAssertions community fork).

## Run

```powershell
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
dotnet run --project src/AgentAssist.Api
```

## Configuration: Mock vs DevCloud

`AgentAssistOptions.Mode` selects the runtime composition:

- `Mock` — deterministic in-memory knowledge, mock chat client (also returns structured JSON), `.AllowAnonymous()` endpoints, no Azure resources required.
- `DevCloud` — Azure AI Search retrieval, Azure OpenAI `IChatClient`, Azure SQL audit, Application Insights / OpenTelemetry. Configure via `dotnet user-secrets` locally and Key Vault in Azure.

### Identity — no API keys (local + pilot)

Neither local development nor the internal pilot uses Search/OpenAI **API keys**. Data-plane access uses **DefaultAzureCredential** (local: `az login`) and **Microsoft Entra ID RBAC** on Azure resources.

Roles documented in [`docs/azure/production-pilot-azure-setup.md`](docs/azure/production-pilot-azure-setup.md) (Managed Identity on App Service):

| Service | Role |
|---|---|
| Azure AI Search | Search Index Data Contributor |
| Azure OpenAI | Cognitive Services OpenAI User |
| Key Vault | Key Vault Secrets User |
| Azure SQL (audit DB) | `CREATE USER [<app-service>] FROM EXTERNAL PROVIDER` + `db_datareader` / `db_datawriter` |

Local development never reads secrets from `appsettings.json` (committed) or `appsettings.Development.json` (gitignored). Use `dotnet user-secrets`:

```powershell
cd src/AgentAssist.Api
dotnet user-secrets init
dotnet user-secrets set "AgentAssist:Mode" "DevCloud"
dotnet user-secrets set "AzureSearch:Endpoint" "https://<your-search>.search.windows.net"
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://<your-openai>.openai.azure.com"
dotnet user-secrets set "AzureOpenAI:ChatDeploymentName" "<your-chat-deployment>"
# Optional — only if you enable vector/hybrid retrieval (see below)
dotnet user-secrets set "AzureOpenAI:EmbeddingDeploymentName" "<your-embed-deployment>"
dotnet user-secrets set "AzureSql:ConnectionString" "Server=...;Authentication=Active Directory Default"
dotnet user-secrets set "ApplicationInsights:ConnectionString" "InstrumentationKey=...;IngestionEndpoint=..."
```

Full Azure provisioning steps: [`docs/azure/production-pilot-azure-setup.md`](docs/azure/production-pilot-azure-setup.md).

### Embedding / vector retrieval (optional)

Bicep and the setup guide can provision an **embedding** deployment, and `EmbeddingDeploymentName` is supported in configuration. The **production pilot evaluation** ran **semantic-only** search (`semanticOnly: true` in Layer 1 results; DevCloud eval clears `VectorFieldName` and `EmbeddingDeploymentName` unless `EVAL_SEMANTIC_ONLY=false`). You do not need `EmbeddingDeploymentName` for semantic-only retrieval; set it only when experimenting with hybrid/vector search.

## Pilot User Context

The internal pilot uses a header-based `IUserContextProvider`. Production-grade authentication is intentionally **out of scope** (see ADR-0010). Pass these headers with every assistant request:

| Header | Purpose | Example |
|---|---|---|
| `X-Agent-User` | User identifier surfaced to audit | `pilot-user` |
| `X-Agent-Roles` | Comma-separated allow-listed roles used for retrieval filter | `agent,supervisor` |
| `X-Agent-Location` | Optional location filter for retrieval | `branch-a` |
| `X-Correlation-Id` | Correlates request with audit and Application Insights traces | any string |

Header context is registered only when the host environment is `Development` or `InternalPilot`. Outside those environments the app falls back to a fixed `MockUserContextProvider` and logs a startup warning.

## Example Requests

The request body is strict: only `question` is accepted. Identity (user, roles, location) flows exclusively through the `X-Agent-*` headers; submitting `userId` or `roles` in the body returns `400 Bad Request` with a ProblemDetails message that points to the expected headers (see ADR-0010).

```powershell
curl.exe -X POST "https://localhost:5001/api/v1/assistant/query" -H "Content-Type: application/json" -H "X-Correlation-Id: demo-123" -H "X-Agent-User: pilot-user" -H "X-Agent-Roles: agent" -H "X-Agent-Location: branch-a" -d "{\"question\":\"MR randevu hazırlık bilgisi nedir?\"}"
```

```powershell
curl.exe -X POST "https://localhost:5001/api/v1/assistant/query" -H "Content-Type: application/json" -H "X-Agent-User: pilot-user" -H "X-Agent-Roles: agent" -d "{\"question\":\"tamamen alakasız bilinmeyen konu\"}"
```

## Deploy to Azure (Internal Pilot)

1. Provision Azure resources via the Bicep template:

```powershell
az group create -n <your-rg> -l westeurope
az deployment group create `
  --resource-group <your-rg> `
  --template-file infra/main.bicep `
  --parameters infra/main.bicepparam `
  --parameters sqlAdminPassword='<replace-with-strong-password>'
```

`sqlAdminPassword` is required by [`infra/main.bicep`](infra/main.bicep) to create the SQL **server** (`administratorLogin` / `administratorLoginPassword` on `Microsoft.Sql/servers`). That SQL login is for server administration and initial setup only. The **application** connects with **Entra ID only** (`Authentication=Active Directory Default` in the connection string; managed identity + `CREATE USER … FROM EXTERNAL PROVIDER` per the setup guide). No SQL password is stored in app configuration.

2. Create the Azure AI Search index (see [`docs/azure/search-index-schema.md`](docs/azure/search-index-schema.md)) and the chat deployment inside Azure OpenAI; embedding deployment is **optional** for semantic-only pilot (see [Embedding / vector retrieval](#embedding--vector-retrieval-optional)).

3. Apply the EF Core migration to the audit database (the production pilot does not commit migrations; create one locally then apply):

```powershell
dotnet ef migrations add InitialAuditSchema `
  --project src/AgentAssist.Infrastructure `
  --startup-project src/AgentAssist.Api
dotnet ef database update `
  --project src/AgentAssist.Infrastructure `
  --startup-project src/AgentAssist.Api
```

4. Publish and deploy the app:

```powershell
dotnet publish src/AgentAssist.Api -c Release -o out
az webapp deploy `
  --resource-group <your-rg> `
  --name <your-app-service> `
  --src-path out
```

5. Smoke test with pilot headers (`.AllowAnonymous()` endpoints; production hardening required before exposing further — ADR-0010):

```powershell
$app = "https://<your-app-service>.azurewebsites.net"
curl.exe -X POST "$app/api/v1/assistant/query" `
  -H "Content-Type: application/json" `
  -H "X-Correlation-Id: smoke-1" `
  -H "X-Agent-User: pilot-user" `
  -H "X-Agent-Roles: agent" `
  -H "X-Agent-Location: branch-a" `
  -d '{"question":"MR randevu hazırlık bilgisi nedir?"}'
curl.exe "$app/health/live"
curl.exe "$app/health/ready"
```

6. Tear down at the end of the pilot:

```powershell
az group delete --name <your-rg> --yes --no-wait
```

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
