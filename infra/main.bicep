// Voice of the Game: one environment (dev, demo).
// Deploy with infra/provision.ps1 (Azure CLI). The AI resource has local (key) auth disabled, so every
// call uses Microsoft Entra ID. The one secret, the MCP key, goes into Key Vault and the Foundry
// project connection; it is never an output.
targetScope = 'resourceGroup'

@description('Short environment name, used in resource names (dev, demo).')
@maxLength(8)
param environmentName string = 'dev'

@description('Azure region. Sweden Central supports neural, HD and MAI voices, Voice Live and Foundry agents.')
param location string = resourceGroup().location

@description('Object ID of the developer who runs the apps locally (resolved by provision.ps1 from the signed-in user).')
param developerPrincipalId string

@allowed(['User', 'ServicePrincipal', 'Group'])
param developerPrincipalType string = 'User'

@description('Key the Foundry agents present to the gateway\'s MCP endpoint. provision.ps1 reuses the stored one.')
@secure()
param mcpApiKey string

@description('Gateway image in the registry; empty before the first build.')
param gatewayImage string = ''

@description('Commentary languages with a companion agent; each gets its own MCP connection (see below).')
param companionLanguages array = ['en-GB', 'es-ES']

var suffix = take(uniqueString(resourceGroup().id), 6)
var name = 'vog-${environmentName}'
var tags = {
  project: 'voice-of-the-game'
  environment: environmentName
}

module identity 'modules/identity.bicep' = {
  name: 'identity'
  params: { location: location, name: 'id-${name}-gateway', tags: tags }
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: { location: location, name: name, tags: tags }
}

module registry 'modules/container-registry.bicep' = {
  name: 'container-registry'
  params: { location: location, name: 'crvog${environmentName}${suffix}', tags: tags, pullPrincipalId: identity.outputs.principalId }
}

module vault 'modules/key-vault.bicep' = {
  name: 'key-vault'
  params: {
    location: location
    name: 'kv-${name}-${suffix}'
    tags: tags
    readerPrincipalId: identity.outputs.principalId
    developerPrincipalId: developerPrincipalId
    developerPrincipalType: developerPrincipalType
    mcpApiKey: mcpApiKey
  }
}

module foundry 'modules/ai-foundry.bicep' = {
  name: 'ai-foundry'
  params: {
    location: location
    accountName: '${name}-${suffix}'
    projectName: name
    developerPrincipalId: developerPrincipalId
    developerPrincipalType: developerPrincipalType
    appPrincipalId: identity.outputs.principalId
    tags: tags
  }
}

module apps 'modules/container-apps.bicep' = {
  name: 'container-apps'
  params: {
    location: location
    name: name
    tags: tags
    identityId: identity.outputs.identityId
    registryLoginServer: registry.outputs.loginServer
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    image: gatewayImage
    settings: [
      { name: 'VOG_AI_ENDPOINT', value: foundry.outputs.cognitiveServicesEndpoint }
      { name: 'VOG_AI_SERVICES_ENDPOINT', value: foundry.outputs.aiServicesEndpoint }
      { name: 'VOG_FOUNDRY_PROJECT_ENDPOINT', value: foundry.outputs.projectEndpoint }
      { name: 'VOG_KEY_VAULT_URI', value: vault.outputs.uri }
      // AzureCredentials uses the managed identity when this (and IDENTITY_ENDPOINT, set by the platform) is present.
      { name: 'AZURE_CLIENT_ID', value: identity.outputs.clientId }
      { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: monitoring.outputs.appInsightsConnectionString }
    ]
  }
}

// One connection per language. Foundry Agent Service sends MCP requests to the connection's target,
// not to the tool's own server URL, so the language-specific path has to be the target itself.
module mcpConnections 'modules/foundry-connection.bicep' = [for lang in companionLanguages: {
  name: 'foundry-mcp-${toLower(lang)}'
  params: {
    accountName: foundry.outputs.accountName
    projectName: foundry.outputs.projectName
    connectionName: 'vog-match-mcp-${toLower(lang)}'
    target: '${apps.outputs.url}/mcp/${lang}'
    mcpApiKey: mcpApiKey
  }
}]

output AZURE_LOCATION string = location
output VOG_AI_ACCOUNT_NAME string = foundry.outputs.accountName
output VOG_AI_ENDPOINT string = foundry.outputs.cognitiveServicesEndpoint
output VOG_AI_SERVICES_ENDPOINT string = foundry.outputs.aiServicesEndpoint
output VOG_FOUNDRY_PROJECT_ENDPOINT string = foundry.outputs.projectEndpoint
output VOG_FOUNDRY_PROJECT_NAME string = foundry.outputs.projectName
output VOG_AGENT_MODEL_DEPLOYMENT string = foundry.outputs.agentModelDeployment
output VOG_AGENT_GUARDRAIL_ID string = foundry.outputs.agentGuardrailId
output VOG_KEY_VAULT_NAME string = vault.outputs.name
output VOG_KEY_VAULT_URI string = vault.outputs.uri
output VOG_REGISTRY_NAME string = registry.outputs.name
output VOG_GATEWAY_APP string = apps.outputs.appName
output VOG_GATEWAY_URL string = apps.outputs.url
output VOG_MCP_URL string = '${apps.outputs.url}/mcp'
// Connections are named <prefix>-<lang>, for example vog-match-mcp-en-gb.
output VOG_MCP_CONNECTION_PREFIX string = 'vog-match-mcp'
