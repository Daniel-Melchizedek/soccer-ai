// The Foundry project connection behind the agents' MCP tool: it holds the gateway's MCP key, so the
// key never appears in an agent definition. As certflow-ai found, the category must be 'RemoteTool':
// 'CustomKeys' authenticates too, but the portal's Tools blade then can't see the server.
param accountName string
param projectName string
param connectionName string
param target string

@secure()
param mcpApiKey string

resource account 'Microsoft.CognitiveServices/accounts@2025-06-01' existing = {
  name: accountName
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' existing = {
  parent: account
  name: projectName
}

#disable-next-line BCP036 // 'RemoteTool' is accepted by the service but not yet in the type definitions.
resource connection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-06-01' = {
  parent: project
  name: connectionName
  properties: {
    category: 'RemoteTool'
    target: target
    authType: 'CustomKeys'
    isSharedToAll: true
    // The header the gateway checks on every MCP request.
    credentials: { keys: { 'X-Api-Key': mcpApiKey } }
  }
}

output name string = connection.name
