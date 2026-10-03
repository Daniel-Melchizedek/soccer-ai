// The only secret in the system: the key Foundry presents to the gateway's MCP endpoint. The gateway
// reads it here at startup (managed identity in Azure, your Azure CLI sign-in locally), and the same
// value sits in the Foundry project connection. provision.ps1 reuses it across runs, because minting
// a new one would break every agent tool call until the connection caught up.
param location string
param name string
param tags object
param readerPrincipalId string
param developerPrincipalId string
param developerPrincipalType string

@secure()
param mcpApiKey string

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    softDeleteRetentionInDays: 7
  }
}

// Secret names map to configuration keys with "--" for ":" (Mcp--ApiKey -> Mcp:ApiKey).
resource mcpKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'Mcp--ApiKey'
  properties: { value: mcpApiKey }
}

var secretsUser = '4633458b-17de-408a-b874-0445c86b69e6'
var secretsOfficer = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'

resource appReads 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, readerPrincipalId, secretsUser)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsUser)
    principalId: readerPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// The developer runs the gateway locally (reads the key) and provision.ps1 (reuses it).
resource developerManages 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, developerPrincipalId, secretsOfficer)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsOfficer)
    principalId: developerPrincipalId
    principalType: developerPrincipalType
  }
}

output name string = vault.name
output uri string = vault.properties.vaultUri
