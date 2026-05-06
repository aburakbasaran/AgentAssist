# Agent Assist Infrastructure (Bicep)

This folder contains the Bicep templates that provision the Agent Assist production pilot resources. The reference architecture targets an internal pilot; production hardening (Entra ID, APIM, Private Endpoints) is intentionally out of scope (see [`docs/adr/0010-pilot-user-context-production-auth-out-of-scope.md`](../docs/adr/0010-pilot-user-context-production-auth-out-of-scope.md)).

## Files

| File | Purpose |
|---|---|
| `main.bicep` | Resource Group-scoped template provisioning Search, OpenAI, SQL, Storage, Key Vault, App Service, Application Insights, and Log Analytics. |
| `main.bicepparam` | Parameter file with sane defaults (region, tier, deployment names). Override per environment. |

## Prerequisites

- Azure CLI 2.61+ (`az version`).
- An empty resource group (`az group create -n <your-rg> -l westeurope`).
- Quota for Azure OpenAI in the chosen region.
- Permissions to create role assignments at the subscription scope (or scope deploys to the resource group with a privileged identity).

## Deploy

```bash
az deployment group create \
  --resource-group <your-rg> \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam \
  --parameters sqlAdminPassword='<replace-with-strong-password>'
```

The deployment provisions every resource and emits outputs for the values that the application needs (App Service URL, Search endpoint, OpenAI endpoint, Key Vault URI, App Service principal id).

After the deployment completes, you must:

1. Create the Azure AI Search index using [`docs/azure/search-index-schema.md`](../docs/azure/search-index-schema.md).
2. Create the chat and embedding deployments inside the Azure OpenAI resource (Slice 1 setup guide, section 4).
3. Run the EF Core migration once against the audit database (`dotnet ef database update --project src/AgentAssist.Infrastructure --startup-project src/AgentAssist.Api`). Note: the production pilot does not commit a migration; run `dotnet ef migrations add InitialAuditSchema` once locally and apply it.
4. Grant the App Service managed identity the SQL roles documented in [`docs/azure/production-pilot-azure-setup.md`](../docs/azure/production-pilot-azure-setup.md) section "Managed Identity Role Assignments".

## App Configuration

The deployment configures the App Service with these environment variables (Bicep `appSettings`):

- `AgentAssist__Mode = DevCloud`
- `AgentAssist__AllowHeaderUserContext = false` (defensive default; override locally if a pilot is being demoed against the deployed app)
- `AzureSearch__Endpoint = <search-endpoint>`
- `AzureOpenAI__Endpoint = <openai-endpoint>`
- `ApplicationInsights__ConnectionString = <ai-connection-string>`

Sensitive values (chat/embedding deployment names, SQL connection string, Application Insights connection string) are stored in Key Vault and surfaced to the App Service via Key Vault references.

## Smoke Tests

After publishing the app:

```powershell
$app = "https://<app-service-name>.azurewebsites.net"

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

The pilot user context flows via the `X-Agent-*` headers; production deployments must add Entra ID before exposing the app to untrusted networks (ADR-0010).

## Tear Down

```bash
az group delete --name <your-rg> --yes --no-wait
```

This removes every resource provisioned by the template. **Always tear down at the end of the pilot to avoid ongoing cost.**
