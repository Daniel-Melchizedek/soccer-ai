#requires -Version 7.0
<#
.SYNOPSIS
    Provisions and deploys the Voice of the Game environment with the Azure CLI.

.DESCRIPTION
    1. Creates or updates the resource group and deploys infra/main.bicep: the Foundry resource and
       project (model, guardrail), Key Vault, registry, monitoring and the gateway container app.
    2. Builds the gateway image in Azure (`az acr build`, no local Docker) and rolls it out.
    3. Writes the non-secret settings to .env.local at the repository root (git-ignored).
    4. Registers the Foundry agents (tools/Vog.FoundrySetup) against the deployed MCP endpoint.
    5. Seeds historical aggregates and launches quality evaluations and managed red-team scans.

    The one secret, the key Foundry presents to the gateway's MCP endpoint, is generated once, kept in
    Key Vault and reused on every later run: a new key would break the agents' tool calls until the
    Foundry connection caught up. Infrastructure is repeatable; agent/evaluator versions and evaluation
    runs are new on each invocation. Matching soft-deleted resources are recovered, never purged.

.EXAMPLE
    ./infra/provision.ps1

.EXAMPLE
    ./infra/provision.ps1 -SkipBuild     # infrastructure only; keeps the gateway image already deployed
#>
[CmdletBinding()]
param(
    [ValidatePattern('^[a-z0-9]{1,8}$')]
    [string] $Environment = 'dev',

    [string] $Location = 'swedencentral',

    [string] $ResourceGroup,

    [string] $Subscription,

    [switch] $SkipBuild,

    [switch] $SkipEvaluations,

    [switch] $SkipRedTeam,

    [string] $EvaluationModelName = 'gpt-4.1',

    [string] $EvaluationModelVersion = '2025-04-14',

    [ValidateRange(1, 1000)]
    [int] $EvaluationModelCapacity = 30,

    [string] $EvaluationOutputDirectory,

    [string] $RedTeamOutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not $SkipEvaluations -and -not $SkipRedTeam -and $RedTeamOutputDirectory -and (Test-Path $RedTeamOutputDirectory)) {
    if (@(Get-ChildItem $RedTeamOutputDirectory -Force).Count) {
        throw 'Use a new empty RedTeamOutputDirectory, or collect saved runs with scripts/redteam_agents.ps1 -CollectOnly.'
    }
}

if (-not $ResourceGroup) {
    $ResourceGroup = "rg-vog-$Environment"
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$placeholderImage = 'mcr.microsoft.com/dotnet/samples:aspnetapp'

function Invoke-Az {
    $output = & az @args
    if ($LASTEXITCODE -ne 0) {
        throw "az $($args[0..1] -join ' ') failed with exit code $LASTEXITCODE."
    }

    return $output
}

# Deploys main.bicep. The MCP key travels in a temporary parameters file, not on the command line,
# where other local processes could read it.
function Deploy-Infrastructure([string] $Image, [string] $Label) {
    $parameters = @{
        '$schema'      = 'https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#'
        contentVersion = '1.0.0.0'
        parameters     = @{
            environmentName      = @{ value = $Environment }
            developerPrincipalId = @{ value = $principalId }
            mcpApiKey            = @{ value = $mcpApiKey }
            gatewayImage         = @{ value = $Image }
            evaluationModelName = @{ value = $EvaluationModelName }
            evaluationModelVersion = @{ value = $EvaluationModelVersion }
            evaluationModelCapacity = @{ value = $EvaluationModelCapacity }
        }
    }

    $file = Join-Path ([IO.Path]::GetTempPath()) "vog-$Environment-$([Guid]::NewGuid().ToString('n')).parameters.json"
    try {
        $parameters | ConvertTo-Json -Depth 5 | Set-Content -Path $file -Encoding utf8NoBOM
        return Invoke-Az deployment group create `
            --resource-group $ResourceGroup `
            --name "vog-$Environment-$Label-$(Get-Date -Format 'yyyyMMdd-HHmmss')" `
            --template-file (Join-Path $PSScriptRoot 'main.bicep') `
            --parameters "@$file" `
            --query properties.outputs `
            --output json | ConvertFrom-Json
    }
    finally {
        Remove-Item $file -Force -ErrorAction SilentlyContinue
    }
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    $cliDirectory = Join-Path ${env:ProgramFiles} 'Microsoft SDKs/Azure/CLI2/wbin'
    if (Test-Path (Join-Path $cliDirectory 'az.cmd')) { $env:PATH = "$cliDirectory;$env:PATH" }
    else { throw 'The Azure CLI (az) is not installed. See https://aka.ms/installazurecli' }
}
foreach ($command in @('dotnet', 'git')) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) { throw "$command is required." }
}
$historicalFiles = @('goals-per-decade.json', 'home-advantage.json')
foreach ($file in $historicalFiles) {
    $data = Get-Content (Join-Path $repoRoot "data/historical/$file") -Raw -Encoding UTF8 | ConvertFrom-Json
    if (@($data).Count -eq 0) { throw "Empty historical seed: $file" }
}

if ($Subscription) {
    Invoke-Az account set --subscription $Subscription | Out-Null
}

$account = Invoke-Az account show --query '{name:name, user:user.name}' --output json | ConvertFrom-Json
Write-Host "Subscription   : $($account.name)"
Write-Host "Signed in as   : $($account.user)"
Write-Host "Region         : $Location"
Write-Host "Resource group : $ResourceGroup"

$principalId = Invoke-Az ad signed-in-user show --query id --output tsv

Write-Host "`n[1/8] Resource group and soft-delete recovery"
$resourceGroupId = Invoke-Az group create --name $ResourceGroup --location $Location --tags project=voice-of-the-game environment=$Environment --query id --output tsv
$deletedVaults = Invoke-Az keyvault list-deleted --query '[].{name:name,originalId:properties.vaultId}' --output json | ConvertFrom-Json
$recoveredVault = $false
foreach ($deleted in @($deletedVaults)) {
    if ($deleted.originalId -like "$resourceGroupId/*" -and $deleted.name -like "kv-vog-$Environment-*") {
        Invoke-Az keyvault recover --name $deleted.name --resource-group $ResourceGroup --output none
        $recoveredVault = $true
    }
}
$deletedAccounts = Invoke-Az cognitiveservices account list-deleted --query '[].{name:name,location:location,id:id}' --output json | ConvertFrom-Json
foreach ($deleted in @($deletedAccounts)) {
    if ($deleted.id -match "/resourceGroups/$([regex]::Escape($ResourceGroup))/deletedAccounts/" -and $deleted.name -like "vog-$Environment-*") {
        Invoke-Az cognitiveservices account recover --name $deleted.name --resource-group $ResourceGroup --location $deleted.location --output none
    }
}

Write-Host '[2/8] MCP key'
$vaultName = Invoke-Az keyvault list --resource-group $ResourceGroup --query '[0].name' --output tsv
$mcpApiKey = $null
$currentImage = Invoke-Az containerapp list --resource-group $ResourceGroup --query "[?name=='ca-vog-$Environment-gateway'].properties.template.containers[0].image | [0]" --output tsv
if ($recoveredVault -and $currentImage) {
    throw 'Recovered a vault while a gateway still exists. Restore vault access and rerun before rotating its MCP key.'
}
if ($vaultName -and -not $recoveredVault) {
    $mcpApiKey = Invoke-Az keyvault secret show --vault-name $vaultName --name 'Mcp--ApiKey' --query value --output tsv
    if (-not $mcpApiKey) { throw 'Existing MCP key is empty; refusing to silently rotate it.' }
}

if ($mcpApiKey) {
    Write-Host "  reusing the key in $vaultName"
}
else {
    $mcpApiKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
    Write-Host '  generated a new key (fresh environment)'
}

if (-not $currentImage -or $currentImage -eq $placeholderImage) {
    $currentImage = ''
}
if ($SkipBuild -and -not $currentImage) { throw '-SkipBuild requires an already deployed gateway image.' }

Write-Host '[3/8] Deploying infra/main.bicep (several minutes the first time)'
$outputs = Deploy-Infrastructure -Image $currentImage -Label 'infra'

if (-not $SkipBuild) {
    $registry = $outputs.VOG_REGISTRY_NAME.value
    $tag = "$(git -C $repoRoot rev-parse --short HEAD)-$(Get-Date -Format 'yyyyMMddHHmmss')"
    Write-Host "[4/8] Building vog-gateway:$tag in $registry and rolling it out"
    Invoke-Az acr build `
        --registry $registry `
        --resource-group $ResourceGroup `
        --image "vog-gateway:$tag" `
        --file (Join-Path $repoRoot 'src/Vog.Gateway/Dockerfile') `
        $repoRoot | Out-Host
    $loginServer = Invoke-Az acr show --name $registry --resource-group $ResourceGroup --query loginServer --output tsv
    $outputs = Deploy-Infrastructure -Image "$loginServer/vog-gateway:$tag" -Label 'gateway'
}
else {
    Write-Host '[4/8] Skipping the image build (-SkipBuild)'
}

Write-Host '      Writing .env.local (git-ignored; endpoints and names only, no keys)'
$lines = @('# Generated by infra/provision.ps1. Endpoints and names only: the MCP key stays in Key Vault.')
foreach ($output in $outputs.PSObject.Properties) {
    # The CLI camel-cases output names (AZURE_LOCATION -> azurE_LOCATION); they are UPPER_SNAKE by design.
    $lines += "$($output.Name.ToUpperInvariant())=$($output.Value.value)"
    [Environment]::SetEnvironmentVariable($output.Name.ToUpperInvariant(), [string]$output.Value.value, 'Process')
}

Set-Content -Path (Join-Path $repoRoot '.env.local') -Value $lines -Encoding utf8NoBOM

Write-Host '[5/8] Seeding historical data'
foreach ($file in $historicalFiles) {
    Invoke-Az storage blob upload --account-name $outputs.VOG_STORAGE_ACCOUNT_NAME.value `
        --container-name vog-data --name "public/$file" --file (Join-Path $repoRoot "data/historical/$file") `
        --auth-mode login --overwrite true --content-type application/json --output none
}

Write-Host '[6/8] Checking gateway health'
Invoke-WebRequest -Uri "$($outputs.VOG_GATEWAY_URL.value)/healthz" -TimeoutSec 30 `
    -MaximumRetryCount 12 -RetryIntervalSec 10 | Out-Null

Write-Host '[7/8] Registering the Foundry agents'
Push-Location $repoRoot
try {
    dotnet run --project (Join-Path $repoRoot 'tools/Vog.FoundrySetup') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Registering the Foundry agents failed.' }
}
finally { Pop-Location }

Write-Host '[8/8] Foundry evaluations and red teaming'
if (-not $SkipEvaluations) {
    $evaluationParameters = @{
        ProjectEndpoint = $outputs.VOG_FOUNDRY_PROJECT_ENDPOINT.value
        JudgeDeployment = $outputs.VOG_EVALUATION_MODEL_DEPLOYMENT.value
    }
    if ($EvaluationOutputDirectory) { $evaluationParameters.OutputDirectory = $EvaluationOutputDirectory }
    & (Join-Path $repoRoot 'scripts/evaluate_agents.ps1') @evaluationParameters
}
else {
    Write-Host 'Skipping evaluation runs (-SkipEvaluations); judge infrastructure remains deployed.'
}

if (-not $SkipEvaluations -and -not $SkipRedTeam) {
    $redTeamParameters = @{
        ProjectEndpoint = $outputs.VOG_FOUNDRY_PROJECT_ENDPOINT.value
        JudgeDeployment = $outputs.VOG_EVALUATION_MODEL_DEPLOYMENT.value
    }
    if ($RedTeamOutputDirectory) { $redTeamParameters.OutputDirectory = $RedTeamOutputDirectory }
    & (Join-Path $repoRoot 'scripts/redteam_agents.ps1') @redTeamParameters
}
else {
    Write-Host 'Skipping red-team scans (-SkipEvaluations or -SkipRedTeam); tracing remains enabled.'
}

Write-Host "`nDone. New role assignments can take a few minutes to take effect."
foreach ($output in $outputs.PSObject.Properties) {
    Write-Host ('  {0,-30} {1}' -f $output.Name.ToUpperInvariant(), $output.Value.value)
}
