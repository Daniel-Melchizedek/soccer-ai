// Microsoft Foundry resource (AIServices) for Speech, Voice Live and, later, the hosted agents.
// Ported from certflow-ai's ai-foundry.bicep, with one deliberate change: disableLocalAuth.
// The account has no usable keys, so nothing secret can leak into config or the repo.
param location string
param accountName string
param projectName string
param developerPrincipalId string
param developerPrincipalType string
param tags object

resource account 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: accountName
  location: location
  tags: tags
  kind: 'AIServices'
  sku: { name: 'S0' }
  identity: { type: 'SystemAssigned' }
  properties: {
    // allowProjectManagement enables the Foundry projects child resource; customSubDomainName gives
    // the <name>.cognitiveservices.azure.com / .services.ai.azure.com hosts that Entra auth requires.
    allowProjectManagement: true
    customSubDomainName: accountName
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: true
  }
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: account
  name: projectName
  location: location
  tags: tags
  identity: { type: 'SystemAssigned' }
  properties: {
    displayName: 'Voice of the Game'
    description: 'Accessibility-first multilingual AI match commentary (Inside the Game hackathon)'
  }
}

// Data-plane roles for the developer running the apps locally with DefaultAzureCredential.
var developerRoles = {
  // Text to speech and speech to text with an Entra token.
  speechUser: 'f2dc8367-1007-4938-bd23-fe263f013447'
  // Voice Live and other Foundry Tools data-plane calls.
  cognitiveServicesUser: 'a97b65f3-24c7-4388-baec-2e87135dc908'
  // Foundry projects, agents and model deployments (formerly "Azure AI User").
  foundryUser: '53ca6127-db72-4b80-b1b0-d745d6d5456d'
}

resource developerRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for role in items(developerRoles): {
  name: guid(account.id, developerPrincipalId, role.value)
  scope: account
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', role.value)
    principalId: developerPrincipalId
    principalType: developerPrincipalType
  }
}]

output accountName string = account.name
output accountId string = account.id
// Speech SDK: SpeechConfig.FromEndpoint(new Uri(cognitiveServicesEndpoint), credential).
output cognitiveServicesEndpoint string = account.properties.endpoint
// Voice Live (Azure.AI.VoiceLive) endpoint.
output aiServicesEndpoint string = 'https://${accountName}.services.ai.azure.com/'
output projectEndpoint string = 'https://${accountName}.services.ai.azure.com/api/projects/${projectName}'
