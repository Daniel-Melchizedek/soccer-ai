// The gateway: the live match, the listener page, the companion relay and the MCP endpoint for the
// Foundry agents. One replica, always on: the match and SignalR connections live in memory, and
// Foundry Agent Service must be able to reach the MCP endpoint whenever a fan asks a question.
param location string
param name string
param tags object
param identityId string
param registryLoginServer string
param logAnalyticsWorkspaceId string

@description('Settings for the gateway: endpoints and the identity client id. No secrets: the MCP key is read from Key Vault.')
param settings array

@description('The gateway image in the registry. Empty on the very first run, before an image exists: a public placeholder is used.')
param image string = ''

var published = !empty(image)

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-${name}'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: reference(logAnalyticsWorkspaceId, '2023-09-01').customerId
        sharedKey: listKeys(logAnalyticsWorkspaceId, '2023-09-01').primarySharedKey
      }
    }
  }
}

resource gateway 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-${name}-gateway'
  location: location
  tags: tags
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${identityId}': {} } }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      // Public: listeners connect from browsers, and Foundry Agent Service calls the MCP endpoint
      // from addresses that can't be enumerated, so the endpoint's API key is what protects it.
      ingress: { external: true, targetPort: 8080, transport: 'auto' }
      registries: [{ server: registryLoginServer, identity: identityId }]
    }
    template: {
      containers: [
        {
          name: 'gateway'
          image: published ? image : 'mcr.microsoft.com/dotnet/samples:aspnetapp'
          env: settings
          resources: { cpu: json('0.5'), memory: '1Gi' }
          probes: published
            ? [
                { type: 'Liveness', httpGet: { path: '/healthz', port: 8080 }, initialDelaySeconds: 10, periodSeconds: 30 }
                { type: 'Readiness', httpGet: { path: '/healthz', port: 8080 }, initialDelaySeconds: 5, periodSeconds: 10 }
              ]
            : []
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 1 }
    }
  }
}

output appName string = gateway.name
output url string = 'https://${gateway.properties.configuration.ingress.fqdn}'
