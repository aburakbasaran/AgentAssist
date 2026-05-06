using './main.bicep'

param namePrefix = 'agentassist'
param appServicePlanSku = 'P0v3'
param searchSku = 'standard'
param openAiSku = 'S0'
param chatDeploymentName = 'agentassist-chat-gpt4omini'
param embeddingDeploymentName = 'agentassist-embed-3l'
param sqlDatabaseSku = 'S0'
param sqlAdminLogin = 'agentassistadmin'
// reason: sqlAdminPassword is intentionally not in this file; pass via -p sqlAdminPassword='<secret>' on the CLI.
param sqlAdminPassword = ''
