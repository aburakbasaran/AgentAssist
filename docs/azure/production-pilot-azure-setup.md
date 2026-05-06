# Azure Setup Guide — Production Pilot (Internal Pilot / Reference Architecture)

This guide walks a developer or platform engineer through provisioning the Azure resources required by the production pilot of Agent Assist. The goal is an internal pilot, not public production; the "Optional production hardening" section at the end lists the additional steps required before exposing this app outside a trusted network.

> **Scope reminder.** Production auth (Entra ID), APIM, and private networking are intentionally out of scope. Endpoints stay `.AllowAnonymous()`. Pilot identity flows through `X-Agent-User`, `X-Agent-Roles`, `X-Agent-Location` headers (see ADR-0010).

> **Resource names below are placeholders.** Tokens such as `<your-rg>`, `<your-search-service>`, `<your-openai-account>`, `<your-sql-server>`, `<your-app-service>`, and similar values are illustrative — they do **not** match any real tenant or deployment. Replace them with names you choose for your own subscription before running any command. Names must follow each Azure resource's normal naming rules (lengths, allowed characters, global uniqueness for some).

## Prerequisites

- An Azure subscription with quota for Azure OpenAI in the chosen region.
- Azure CLI 2.61 or newer (`az version`).
- .NET SDK 10.0.x (`dotnet --info` should report `10.0.*`).
- PowerShell 7+ on Windows or Bash on macOS/Linux.
- A non-production sample data set; do **not** load real PHI/PII.

## Defaults

| Setting | Default | Notes |
|---|---|---|
| Region | `westeurope` | Use a region with Azure OpenAI access. |
| Resource Group | `<your-rg>` | Single RG holds every resource. |
| Azure AI Search tier | `standard` (S1) | Sufficient for semantic + vector. |
| Chat deployment | `<your-chat-deployment>` | Placeholder; pick whichever chat model your subscription provides. |
| Embedding deployment | `<your-embed-deployment>` | Default model: `text-embedding-3-large`; switch to `text-embedding-3-small` to lower cost. |
| App Service Plan | Linux P0v3 | One plan per RG. |
| Azure SQL tier | Standard S0 | Audit volume is small. |

## Cost Warning

Each provisioned resource accrues cost. Before running these commands, confirm with your finance owner. Typical pilot resource-group monthly cost (West Europe): **roughly €350-€700**, dominated by App Service Plan, Azure AI Search S1, and Azure OpenAI usage. Always tear the RG down at the end of the pilot (`az group delete --name <rg> --yes`).

## Resource Provisioning

Each resource section below contains: portal path, Azure CLI alternative, the value to copy, whether it is a secret, where it is stored, and how to verify it.

### 1. Resource Group

| Field | Value |
|---|---|
| Portal path | Resource Groups → Create |
| Azure CLI | `az group create -n <your-rg> -l westeurope` |
| Value to copy | Resource group name |
| Secret? | No |
| Stored in | local notes (used in `az ... -g <rg>`) |
| Verification | `az group show -n <your-rg> --query "properties.provisioningState"` returns `Succeeded` |

### 2. Azure AI Search

| Field | Value |
|---|---|
| Portal path | Create resource → AI Search → standard tier |
| Azure CLI | `az search service create -g <your-rg> -n <your-search-service> --sku standard --location westeurope` |
| Value to copy | Endpoint (`https://<your-search-service>.search.windows.net`) |
| Secret? | No (endpoint URL only); admin keys are not used in this pilot — Managed Identity is preferred. |
| Stored in | `appsettings.json` `AzureSearch:Endpoint` (or Key Vault `azureSearch--endpoint`) |
| Verification | `az search service show -g <your-rg> -n <your-search-service> --query "status"` returns `running` |

After provisioning, create the index using the schema documented in `docs/azure/search-index-schema.md` (added in Slice 2).

### 3. Azure OpenAI / Foundry Resource

| Field | Value |
|---|---|
| Portal path | Create resource → Azure OpenAI |
| Azure CLI | `az cognitiveservices account create -g <your-rg> -n <your-openai-account> --kind OpenAI --sku S0 --location westeurope --custom-domain <your-openai-account>` |
| Value to copy | Endpoint (`https://<your-openai-account>.openai.azure.com`) |
| Secret? | No; data-plane access uses Managed Identity. |
| Stored in | `appsettings.json` `AzureOpenAI:Endpoint` (or Key Vault `azureOpenAI--endpoint`) |
| Verification | `az cognitiveservices account show -g <your-rg> -n <your-openai-account> --query "properties.provisioningState"` returns `Succeeded` |

If you have an existing Azure OpenAI resource, use it instead and skip the create step.

### 4. Model Deployments

Two deployments are required: one chat model (gpt-4o or gpt-4o-mini) and one embedding model (text-embedding-3-large or text-embedding-3-small).

```bash
az cognitiveservices account deployment create \
  -g <your-rg> \
  -n <your-openai-account> \
  --deployment-name <your-chat-deployment> \
  --model-name gpt-4o-mini \
  --model-version "2024-07-18" \
  --model-format OpenAI \
  --sku-capacity 30 --sku-name Standard

az cognitiveservices account deployment create \
  -g <your-rg> \
  -n <your-openai-account> \
  --deployment-name <your-embed-deployment> \
  --model-name text-embedding-3-large \
  --model-version "1" \
  --model-format OpenAI \
  --sku-capacity 30 --sku-name Standard
```

| Field | Value |
|---|---|
| Value to copy | Deployment names (`<your-chat-deployment>`, `<your-embed-deployment>`) |
| Secret? | No |
| Stored in | `AzureOpenAI:ChatDeploymentName`, `AzureOpenAI:EmbeddingDeploymentName` |
| Verification | `az cognitiveservices account deployment list -g <your-rg> -n <your-openai-account> --query "[].name"` lists both names |

> **Embedding cost note.** `text-embedding-3-large` produces 3072-dimensional vectors. Storage and ingestion cost in Azure AI Search scales with vector dimensions; switching to `text-embedding-3-small` (1536-dimensional) lowers cost roughly 50% at the price of slightly weaker retrieval quality.

### 5. Storage Account (Knowledge Ingestion)

| Field | Value |
|---|---|
| Portal path | Create storage account → Standard LRS |
| Azure CLI | `az storage account create -g <your-rg> -n <your-storage> --sku Standard_LRS --location westeurope` |
| Value to copy | Storage account name |
| Secret? | No (data-plane uses Managed Identity) |
| Stored in | local notes (used by ingestion scripts in Slice 8) |
| Verification | `az storage account show -g <your-rg> -n <your-storage> --query "provisioningState"` returns `Succeeded` |

> **Knowledge ingestion pipeline.** Automated ingestion is **out of scope** for this sprint. Slice 2's `docs/azure/search-index-schema.md` describes a manual `az search` push approach.

### 6. Key Vault

| Field | Value |
|---|---|
| Portal path | Create Key Vault → RBAC permission model |
| Azure CLI | `az keyvault create -g <your-rg> -n <your-keyvault> --location westeurope --enable-rbac-authorization true` |
| Value to copy | Vault URI (`https://<your-keyvault>.vault.azure.net/`) |
| Secret? | No (URI is not sensitive); secret values inside the vault are sensitive. |
| Stored in | `appsettings.json` `KeyVault:Uri` (added in Slice 8) |
| Verification | `az keyvault show -g <your-rg> -n <your-keyvault> --query "properties.provisioningState"` returns `Succeeded` |

The App Service's system-assigned managed identity must be granted the `Key Vault Secrets User` role on this vault (Slice 8 covers the role assignment).

### 7. Application Insights + Log Analytics

| Field | Value |
|---|---|
| Portal path | Create Log Analytics workspace; then create Application Insights linked to it. |
| Azure CLI | `az monitor log-analytics workspace create -g <your-rg> -n <your-loganalytics> --location westeurope` then `az monitor app-insights component create -g <your-rg> -a <your-appinsights> --location westeurope --workspace <your-loganalytics>` |
| Value to copy | Connection string |
| Secret? | Yes (treated as a secret because it can be used to write telemetry to your tenant) |
| Stored in | Key Vault `applicationInsights--connectionString` |
| Verification | `az monitor app-insights component show -g <your-rg> -a <your-appinsights> --query "connectionString"` returns a `InstrumentationKey=...` string |

### 8. Azure SQL (Audit Store)

| Field | Value |
|---|---|
| Portal path | Create SQL Server, then Database (Standard S0). |
| Azure CLI | `az sql server create -g <your-rg> -n <your-sql-server> --location westeurope --admin-user <your-sql-admin> --admin-password '<replace>'` then `az sql db create -g <your-rg> -s <your-sql-server> -n <your-audit-db> --service-objective S0` |
| Value to copy | Connection string (use `Authentication=Active Directory Default` so Managed Identity is used in Azure) |
| Secret? | Yes |
| Stored in | Key Vault `azureSql--connectionString` |
| Verification | `az sql db show -g <your-rg> -s <your-sql-server> -n <your-audit-db> --query "status"` returns `Online` |

The App Service managed identity needs `db_datareader` and `db_datawriter` role on the audit database; Slice 8 covers the role assignment with `CREATE USER [<app-name>] FROM EXTERNAL PROVIDER`.

### 9. App Service (Linux, P0v3)

| Field | Value |
|---|---|
| Portal path | Create App Service Plan (Linux, P0v3) → Create App Service (Linux, .NET 10) |
| Azure CLI | `az appservice plan create -g <your-rg> -n <your-asp> --is-linux --sku P0v3` then `az webapp create -g <your-rg> -p <your-asp> -n <your-app-service> --runtime "DOTNETCORE:10.0"` |
| Value to copy | App Service URL (`https://<your-app-service>.azurewebsites.net`) |
| Secret? | No |
| Stored in | smoke test scripts |
| Verification | `az webapp show -g <your-rg> -n <your-app-service> --query "state"` returns `Running` |

Enable system-assigned managed identity:

```bash
az webapp identity assign -g <your-rg> -n <your-app-service>
```

## Managed Identity Role Assignments

Use the system-assigned principal of the App Service for data-plane access:

```bash
APP_PRINCIPAL=$(az webapp identity show -g <your-rg> -n <your-app-service> --query principalId -o tsv)

# Azure AI Search data-plane
az role assignment create --assignee "$APP_PRINCIPAL" \
  --role "Search Index Data Contributor" \
  --scope $(az search service show -g <your-rg> -n <your-search-service> --query id -o tsv)

# Azure OpenAI data-plane
az role assignment create --assignee "$APP_PRINCIPAL" \
  --role "Cognitive Services OpenAI User" \
  --scope $(az cognitiveservices account show -g <your-rg> -n <your-openai-account> --query id -o tsv)

# Key Vault secrets
az role assignment create --assignee "$APP_PRINCIPAL" \
  --role "Key Vault Secrets User" \
  --scope $(az keyvault show -g <your-rg> -n <your-keyvault> --query id -o tsv)
```

For Azure SQL, run the following T-SQL from a connected admin session against the audit database:

```sql
CREATE USER [<your-app-service>] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [<your-app-service>];
ALTER ROLE db_datawriter ADD MEMBER [<your-app-service>];
```

## Local Development with `dotnet user-secrets`

Local development uses `DefaultAzureCredential` (which honours `az login`) for Search and OpenAI; pure-config values can be set via `dotnet user-secrets` so they never end up in `appsettings.json`:

```powershell
cd src/AgentAssist.Api
dotnet user-secrets init
dotnet user-secrets set "AzureSearch:Endpoint" "https://<your-search-service>.search.windows.net"
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://<your-openai-account>.openai.azure.com"
dotnet user-secrets set "AzureOpenAI:ChatDeploymentName" "<your-chat-deployment>"
dotnet user-secrets set "AzureOpenAI:EmbeddingDeploymentName" "<your-embed-deployment>"
dotnet user-secrets set "AzureSql:ConnectionString" "Server=tcp:<your-sql-server>.database.windows.net,1433;Initial Catalog=<your-audit-db>;Authentication=Active Directory Default"
dotnet user-secrets set "ApplicationInsights:ConnectionString" "InstrumentationKey=...;IngestionEndpoint=..."
dotnet user-secrets set "AgentAssist:Mode" "DevCloud"
```

`appsettings.json` is committed and contains placeholder values such as `<set-via-keyvault>`. `appsettings.Development.json` is gitignored.

## Pilot Smoke Tests (Headers, not JWT)

```powershell
curl.exe -X POST "https://<your-app-service>.azurewebsites.net/api/v1/assistant/query" `
  -H "Content-Type: application/json" `
  -H "X-Correlation-Id: smoke-1" `
  -H "X-Agent-User: pilot-user" `
  -H "X-Agent-Roles: agent" `
  -H "X-Agent-Location: branch-a" `
  -d '{"question":"MR randevu hazırlık bilgisi nedir?"}'

curl.exe "https://<your-app-service>.azurewebsites.net/health/live"
curl.exe "https://<your-app-service>.azurewebsites.net/health/ready"
```

## Tear Down

```bash
az group delete --name <your-rg> --yes --no-wait
```

This removes every resource provisioned in this guide. Always run this when the pilot ends.

## Optional Production Hardening (Out of Scope this Sprint)

The following items are required before exposing this app outside a trusted internal network. They are documented here for completeness; this reference architecture does **not** implement them:

- **Entra ID app registration + JWT bearer + policy authorization** (e.g. `Microsoft.Identity.Web`). Endpoints would gain `.RequireAuthorization("AssistantAccess")` instead of `.AllowAnonymous()`.
- **APIM in front of App Service** with rate limiting, WAF, JWT validation, and IP allowlist.
- **Private Endpoint / VNet integration** for App Service, Azure SQL, Azure AI Search, Azure OpenAI, and Key Vault, removing public network exposure.
- **Production-mode anonymous fail-fast guard** at startup, ensuring the app cannot run with `.AllowAnonymous()` in `Production`.
- **Automated ingestion pipeline** for knowledge documents (Azure Functions, Azure AI Search indexers, or `azd` push pipelines).
- **Audit-or-fail policy** instead of best-effort, with an outbox pattern in front of Azure SQL.
- **Microsoft Entra Conditional Access** for the App Service identity.

These are tracked in [`docs/quality-gates/production-pilot-quality-gate.md`](../quality-gates/production-pilot-quality-gate.md) and ADR-0010.

