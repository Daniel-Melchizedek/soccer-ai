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

    The one secret, the key Foundry presents to the gateway's MCP endpoint, is generated once, kept in
    Key Vault and reused on every later run: a new key would break the agents' tool calls until the
    Foundry connection caught up. Safe to re-run: every step is idempotent.

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

    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

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
    throw 'The Azure CLI (az) is not installed. See https://aka.ms/installazurecli'
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

Write-Host "`n[1/5] Resource group"
Invoke-Az group create --name $ResourceGroup --location $Location --tags project=voice-of-the-game environment=$Environment --output none

Write-Host '[2/5] MCP key'
$vaultName = Invoke-Az keyvault list --resource-group $ResourceGroup --query '[0].name' --output tsv
$mcpApiKey = $null
if ($vaultName) {
    $mcpApiKey = az keyvault secret show --vault-name $vaultName --name 'Mcp--ApiKey' --query value --output tsv 2>$null
}

if ($mcpApiKey) {
    Write-Host "  reusing the key in $vaultName"
}
else {
    $mcpApiKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
    Write-Host '  generated a new key (fresh environment)'
}

$currentImage = Invoke-Az containerapp list --resource-group $ResourceGroup --query '[0].properties.template.containers[0].image' --output tsv
if (-not $currentImage -or $currentImage -eq $placeholderImage) {
    $currentImage = ''
}

Write-Host '[3/5] Deploying infra/main.bicep (several minutes the first time)'
$outputs = Deploy-Infrastructure -Image $currentImage -Label 'infra'

if (-not $SkipBuild) {
    $registry = $outputs.VOG_REGISTRY_NAME.value
    $tag = "$(git -C $repoRoot rev-parse --short HEAD)-$(Get-Date -Format 'yyyyMMddHHmmss')"
    Write-Host "[4/5] Building vog-gateway:$tag in $registry and rolling it out"
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
    Write-Host '[4/5] Skipping the image build (-SkipBuild)'
}

Write-Host '      Writing .env.local (git-ignored; endpoints and names only, no keys)'
$lines = @('# Generated by infra/provision.ps1. Endpoints and names only: the MCP key stays in Key Vault.')
foreach ($output in $outputs.PSObject.Properties) {
    # The CLI camel-cases output names (AZURE_LOCATION -> azurE_LOCATION); they are UPPER_SNAKE by design.
    $lines += "$($output.Name.ToUpperInvariant())=$($output.Value.value)"
}

Set-Content -Path (Join-Path $repoRoot '.env.local') -Value $lines -Encoding utf8NoBOM

Write-Host '[5/5] Registering the Foundry agents'
dotnet run --project (Join-Path $repoRoot 'tools/Vog.FoundrySetup') | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw 'Registering the Foundry agents failed.'
}

Write-Host "`nDone. New role assignments can take a few minutes to take effect."
foreach ($output in $outputs.PSObject.Properties) {
    Write-Host ('  {0,-30} {1}' -f $output.Name.ToUpperInvariant(), $output.Value.value)
}
