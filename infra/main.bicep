// Voice of the Game: development environment.
// Deploy with infra/provision.ps1 (Azure CLI). Nothing here holds or outputs a secret:
// the AI resource has local (key) auth disabled, so every call uses Microsoft Entra ID.
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

var suffix = take(uniqueString(resourceGroup().id), 6)
var tags = {
  project: 'voice-of-the-game'
  environment: environmentName
}

module foundry 'modules/ai-foundry.bicep' = {
  name: 'ai-foundry'
  params: {
    location: location
    accountName: 'vog-${environmentName}-${suffix}'
    projectName: 'vog-${environmentName}'
    developerPrincipalId: developerPrincipalId
    developerPrincipalType: developerPrincipalType
    tags: tags
  }
}

output AZURE_LOCATION string = location
output VOG_AI_ACCOUNT_NAME string = foundry.outputs.accountName
output VOG_AI_ENDPOINT string = foundry.outputs.cognitiveServicesEndpoint
output VOG_AI_SERVICES_ENDPOINT string = foundry.outputs.aiServicesEndpoint
output VOG_FOUNDRY_PROJECT_ENDPOINT string = foundry.outputs.projectEndpoint
