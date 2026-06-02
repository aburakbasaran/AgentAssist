<#
.SYNOPSIS
    Uploads golden-pilot knowledge documents to Azure AI Search (merge or upload).

.DESCRIPTION
    Reads test/data/azure-search/golden-pilot-knowledge.json and POSTs to the index docs API.
    Configuration from environment (preferred) or Api user-secrets:
      AzureSearch__Endpoint, AzureSearch__IndexName
    Requires: az login, AZURE_TENANT_ID optional but recommended for DefaultAzureCredential chain used by az.

.EXAMPLE
    $env:AZURE_TENANT_ID = (az account show --query tenantId -o tsv)
    ./scripts/Upload-GoldenPilotKnowledge.ps1
#>
param(
    [string]$DocumentsPath = (Join-Path $PSScriptRoot "..\test\data\azure-search\golden-pilot-knowledge.json"),
    [string]$ApiProject = (Join-Path $PSScriptRoot "..\src\AgentAssist.Api\AgentAssist.Api.csproj")
)

$ErrorActionPreference = "Stop"

function Get-ConfigValue([string]$Key) {
    $envName = $Key -replace ':', '__'
    $fromEnv = [Environment]::GetEnvironmentVariable($envName)
    if (-not [string]::IsNullOrWhiteSpace($fromEnv)) { return $fromEnv.Trim() }

    $secrets = dotnet user-secrets list --project $ApiProject 2>$null
    if (-not $secrets) { return $null }
    foreach ($line in $secrets) {
        if ($line -like "$Key = *") {
            return ($line -split '=', 2)[1].Trim()
        }
    }
    return $null
}

$endpoint = Get-ConfigValue "AzureSearch:Endpoint"
$indexName = Get-ConfigValue "AzureSearch:IndexName"
if ([string]::IsNullOrWhiteSpace($endpoint) -or [string]::IsNullOrWhiteSpace($indexName)) {
    throw "AzureSearch:Endpoint and AzureSearch:IndexName must be set via env or user-secrets."
}

if (-not (Test-Path $DocumentsPath)) {
    throw "Documents file not found: $DocumentsPath"
}

$body = Get-Content -Path $DocumentsPath -Raw -Encoding UTF8
$url = ($endpoint.TrimEnd('/')) + "/indexes/$indexName/docs/index?api-version=2024-07-01"

Write-Host "Uploading golden pilot knowledge to $indexName ..."
az rest --method POST --url $url --body $body --resource "https://search.azure.com" --headers "Content-Type=application/json"
Write-Host "Upload completed."
