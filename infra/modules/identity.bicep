// The gateway's identity: pulls its image, reads Key Vault, and calls Speech, Voice Live and Foundry.
param location string
param name string
param tags object

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: name
  location: location
  tags: tags
}

output identityId string = identity.id
output principalId string = identity.properties.principalId
output clientId string = identity.properties.clientId
