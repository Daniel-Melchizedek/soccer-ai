param location string
param name string
param tags object
param appPrincipalId string
param developerPrincipalId string

@allowed(['User', 'ServicePrincipal', 'Group'])
param developerPrincipalType string = 'User'

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  tags: tags
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
  }
}

resource blobs 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {}
}

resource data 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobs
  name: 'vog-data'
  properties: { publicAccess: 'None' }
}

var contributorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')

resource appAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: data
  name: guid(data.id, appPrincipalId, contributorRoleId)
  properties: {
    roleDefinitionId: contributorRoleId
    principalId: appPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource developerAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: data
  name: guid(data.id, developerPrincipalId, contributorRoleId)
  properties: {
    roleDefinitionId: contributorRoleId
    principalId: developerPrincipalId
    principalType: developerPrincipalType
  }
}

output blobEndpoint string = storage.properties.primaryEndpoints.blob
output accountName string = storage.name