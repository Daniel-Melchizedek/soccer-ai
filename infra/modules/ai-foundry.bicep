// Microsoft Foundry resource (AIServices) for Speech, Voice Live and, later, the hosted agents.
// Ported from certflow-ai's ai-foundry.bicep, with one deliberate change: disableLocalAuth.
// The account has no usable keys, so nothing secret can leak into config or the repo.
param location string
param accountName string
param projectName string
param developerPrincipalId string
param developerPrincipalType string
param tags object

@description('The gateway\'s managed identity, which needs the same data-plane roles as the developer.')
param appPrincipalId string

@description('Model deployment used by every Foundry agent.')
param agentModelName string = 'gpt-5.6-terra'
param agentModelVersion string = '2026-07-09'

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

// The one model behind every Foundry agent (BUILD_PLAN.md: a mid-cost model a little above gpt-4o).
// GlobalStandard bills per token used, not per unit reserved, so the 200k TPM ceiling costs nothing idle.
resource agentModel 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: account
  name: agentModelName
  sku: { name: 'GlobalStandard', capacity: 200 }
  properties: {
    model: { format: 'OpenAI', name: agentModelName, version: agentModelVersion }
    versionUpgradeOption: 'NoAutoUpgrade'
  }
}

// Agent-level guardrail, as in certflow-ai: an agent without its own policy inherits only the model's,
// which can't see agent-specific attacks. Fans' questions are untrusted text, so Jailbreak and Indirect
// Attack filters apply, and Task Adherence checks each tool call before it runs. Harm categories sit at
// Medium: Low adds little over the model's own filter and risks refusing ordinary football talk.
// PII filters stay off: player names are the whole point of the tools.
resource agentGuardrail 'Microsoft.CognitiveServices/accounts/raiPolicies@2025-06-01' = {
  parent: account
  name: 'VogAgentGuardrail'
  properties: {
    mode: 'Blocking'
    basePolicyName: 'Microsoft.DefaultV2'
    contentFilters: [
      { name: 'Hate', source: 'Prompt', severityThreshold: 'Medium', blocking: true, enabled: true }
      { name: 'Hate', source: 'Completion', severityThreshold: 'Medium', blocking: true, enabled: true }
      { name: 'Sexual', source: 'Prompt', severityThreshold: 'Medium', blocking: true, enabled: true }
      { name: 'Sexual', source: 'Completion', severityThreshold: 'Medium', blocking: true, enabled: true }
      { name: 'Violence', source: 'Prompt', severityThreshold: 'Medium', blocking: true, enabled: true }
      { name: 'Violence', source: 'Completion', severityThreshold: 'Medium', blocking: true, enabled: true }
      { name: 'Selfharm', source: 'Prompt', severityThreshold: 'Medium', blocking: true, enabled: true }
      { name: 'Selfharm', source: 'Completion', severityThreshold: 'Medium', blocking: true, enabled: true }
      { name: 'Jailbreak', source: 'Prompt', blocking: true, enabled: true }
      { name: 'Indirect Attack', source: 'Prompt', blocking: true, enabled: true }
      { name: 'Indirect Attack Spotlighting', source: 'Prompt', blocking: true, enabled: true }
      { name: 'Task Adherence', source: 'PreToolCall', blocking: true, enabled: true }
      { name: 'Protected Material Text', source: 'Completion', blocking: true, enabled: true }
    ]
  }
  dependsOn: [agentModel]
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
  dependsOn: [agentModel]
}

// Data-plane roles for the developer running the apps locally with DefaultAzureCredential.
var developerRoles = {
  // Text to speech and speech to text with an Entra token.
  speechUser: 'f2dc8367-1007-4938-bd23-fe263f013447'
  // Voice Live and other Foundry Tools data-plane calls.
  cognitiveServicesUser: 'a97b65f3-24c7-4388-baec-2e87135dc908'
  // Foundry projects, agents and model deployments (formerly "Azure AI User").
  foundryUser: '53ca6127-db72-4b80-b1b0-d745d6d5456d'
  // Model inference (Responses) data plane, used by agent runs.
  openAIUser: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
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

resource appRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for role in items(developerRoles): {
  name: guid(account.id, appPrincipalId, role.value)
  scope: account
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', role.value)
    principalId: appPrincipalId
    principalType: 'ServicePrincipal'
  }
}]

output accountName string = account.name
output projectName string = project.name
output accountId string = account.id
// Speech SDK: SpeechConfig.FromEndpoint(new Uri(cognitiveServicesEndpoint), credential).
output cognitiveServicesEndpoint string = account.properties.endpoint
// Voice Live (Azure.AI.VoiceLive) endpoint.
output aiServicesEndpoint string = 'https://${accountName}.services.ai.azure.com/'
output projectEndpoint string = 'https://${accountName}.services.ai.azure.com/api/projects/${projectName}'
output agentModelDeployment string = agentModel.name
// Agents take the guardrail's full ARM id: a bare policy name is accepted but filters nothing.
output agentGuardrailId string = agentGuardrail.id
