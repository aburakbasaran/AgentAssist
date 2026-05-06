# Agent Assist Enterprise .NET Azure Project

**Status:** Phase A delivered (mock vertical slice); production pilot in progress (citation-first RAG over Azure AI Search + Azure OpenAI, internal pilot / reference architecture scope).

Production-grade reference architecture for regulated-industry RAG, in progress, building in public. Phase A is a mock-first vertical slice: it uses deterministic in-memory knowledge, a mock `IChatClient`, embedded prompt templates, and no Azure SDK calls. The production pilot phases swap mocks for Azure AI Search and Azure OpenAI through the same `IKnowledgeSearchService` / `IChatClient` contracts, add structured citation validation, persistent audit, observability, and a health-checked deployment recipe.

The healthcare-flavored documents in this repository are illustrative. They use placeholder names such as Acme Sağlık Grubu, Şube A/B/C, and Doktor X. No real PHI/PII is included.

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

Local development never reads secrets from `appsettings.json` (committed) or `appsettings.Development.json` (gitignored). Use `dotnet user-secrets`:

```powershell
cd src/AgentAssist.Api
dotnet user-secrets init
dotnet user-secrets set "AgentAssist:Mode" "DevCloud"
dotnet user-secrets set "AzureSearch:Endpoint" "https://<your-search>.search.windows.net"
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://<your-openai>.openai.azure.com"
dotnet user-secrets set "AzureOpenAI:ChatDeploymentName" "<your-chat-deployment>"
dotnet user-secrets set "AzureOpenAI:EmbeddingDeploymentName" "<your-embed-deployment>"
dotnet user-secrets set "AzureSql:ConnectionString" "Server=...;Authentication=Active Directory Default"
dotnet user-secrets set "ApplicationInsights:ConnectionString" "InstrumentationKey=...;IngestionEndpoint=..."
```

Full Azure provisioning steps live in [`docs/azure/production-pilot-azure-setup.md`](docs/azure/production-pilot-azure-setup.md).

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

2. Create the Azure AI Search index (see [`docs/azure/search-index-schema.md`](docs/azure/search-index-schema.md)) and the chat/embedding deployments inside Azure OpenAI (see [`docs/azure/production-pilot-azure-setup.md`](docs/azure/production-pilot-azure-setup.md) section 4).

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