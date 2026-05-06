// Agent Assist Production Pilot — Resource Group scoped Bicep template.
// Provisions an internal-pilot environment. Production hardening (Entra ID, APIM, Private Endpoints) is out of scope (ADR-0010).
//
// Usage:
//   az deployment group create -g <rg> -f infra/main.bicep -p infra/main.bicepparam -p sqlAdminPassword='<secret>'

targetScope = 'resourceGroup'

@description('Azure region.')
param location string = resourceGroup().location

@description('Naming prefix used for all child resources. Lowercase, alphanumeric.')
param namePrefix string = 'agentassist'

@description('App Service Plan SKU.')
param appServicePlanSku string = 'P0v3'

@description('Azure AI Search SKU.')
param searchSku string = 'standard'

@description('Azure OpenAI account SKU.')
param openAiSku string = 'S0'

@description('Chat deployment name.')
param chatDeploymentName string = 'agentassist-chat-gpt4omini'

@description('Embedding deployment name.')
param embeddingDeploymentName string = 'agentassist-embed-3l'

@description('Azure SQL Database service objective (e.g. S0).')
param sqlDatabaseSku string = 'S0'

@description('Azure SQL administrator login (used only when the database is recreated).')
param sqlAdminLogin string = 'agentassistadmin'

@secure()
@description('Azure SQL administrator password.')
param sqlAdminPassword string

var searchName = '${namePrefix}-search-pilot'
var openAiName = '${namePrefix}-openai-pilot'
var storageName = take(replace(toLower('${namePrefix}stpilot'), '-', ''), 24)
var keyVaultName = '${namePrefix}-kv-pilot'
var logAnalyticsName = '${namePrefix}-law-pilot'
var appInsightsName = '${namePrefix}-ai-pilot'
var sqlServerName = '${namePrefix}-sql-pilot'
var sqlDatabaseName = 'agentassist-audit'
var appServicePlanName = '${namePrefix}-asp-pilot'
var appServiceName = '${namePrefix}-app-pilot'

resource search 'Microsoft.Search/searchServices@2024-06-01-preview' = {
  name: searchName
  location: location
  sku: {
    name: searchSku
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    semanticSearch: 'standard'
    publicNetworkAccess: 'enabled'
  }
  identity: {
    type: 'SystemAssigned'
  }
}

resource openAi 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: openAiName
  location: location
  kind: 'OpenAI'
  sku: {
    name: openAiSku
  }
  properties: {
    customSubDomainName: openAiName
    publicNetworkAccess: 'Enabled'
  }
  identity: {
    type: 'SystemAssigned'
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2024-01-01' = {
  name: storageName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2024-04-01-preview' = {
  name: keyVaultName
  location: location
  properties: {
    enableRbacAuthorization: true
    enabledForTemplateDeployment: true
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    publicNetworkAccess: 'Enabled'
  }
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }

  resource firewallAzure 'firewallRules@2023-08-01-preview' = {
    name: 'AllowAllAzureIPs'
    properties: {
      startIpAddress: '0.0.0.0'
      endIpAddress: '0.0.0.0'
    }
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: sqlDatabaseSku
    tier: 'Standard'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: appServicePlanSku
    tier: 'PremiumV3'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource appService 'Microsoft.Web/sites@2023-12-01' = {
  name: appServiceName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'AgentAssist__Mode'
          value: 'DevCloud'
        }
        {
          name: 'AgentAssist__AllowHeaderUserContext'
          value: 'false'
        }
        {
          name: 'AzureSearch__Endpoint'
          value: 'https://${searchName}.search.windows.net'
        }
        {
          name: 'AzureSearch__IndexName'
          value: 'agentassist-knowledge'
        }
        {
          name: 'AzureSearch__SemanticConfigurationName'
          value: 'agentassist-semantic'
        }
        {
          name: 'AzureSearch__VectorFieldName'
          value: 'contentVector'
        }
        {
          name: 'AzureOpenAI__Endpoint'
          value: openAi.properties.endpoint
        }
        {
          name: 'AzureOpenAI__ChatDeploymentName'
          value: chatDeploymentName
        }
        {
          name: 'AzureOpenAI__EmbeddingDeploymentName'
          value: embeddingDeploymentName
        }
        {
          name: 'ApplicationInsights__ConnectionString'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'AzureSql__ConnectionString'
          value: 'Server=tcp:${sqlServerName}.database.windows.net,1433;Initial Catalog=${sqlDatabaseName};Authentication=Active Directory Default'
        }
      ]
    }
  }
}

// Role assignments — pilot Managed Identity gets data-plane roles on Search, OpenAI, and Key Vault.
// reason: SQL roles must be granted via T-SQL CREATE USER FROM EXTERNAL PROVIDER (see docs/azure/production-pilot-azure-setup.md).

var searchIndexDataContributorRoleId = '8ebe5a00-799e-43f5-93ac-243d3dce84a7'
var cognitiveServicesOpenAIUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource searchRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: search
  name: guid(search.id, appService.id, searchIndexDataContributorRoleId)
  properties: {
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', searchIndexDataContributorRoleId)
  }
}

resource openAiRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: openAi
  name: guid(openAi.id, appService.id, cognitiveServicesOpenAIUserRoleId)
  properties: {
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesOpenAIUserRoleId)
  }
}

resource keyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, appService.id, keyVaultSecretsUserRoleId)
  properties: {
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
  }
}

output appServiceName string = appService.name
output appServicePrincipalId string = appService.identity.principalId
output appServiceUrl string = 'https://${appService.properties.defaultHostName}'
output searchEndpoint string = 'https://${searchName}.search.windows.net'
output openAiEndpoint string = openAi.properties.endpoint
output keyVaultUri string = keyVault.properties.vaultUri
output applicationInsightsConnectionString string = appInsights.properties.ConnectionString
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
