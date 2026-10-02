#requires -Version 7.0
<#
.SYNOPSIS
    Deletes the Voice of the Game environment created by provision.ps1.

.DESCRIPTION
    Deletes the resource group, then purges any Foundry / AI Services accounts that were in it.
    Deleted accounts are soft-deleted for 48 hours and would otherwise block re-provisioning with
    the same name. Asks for confirmation unless -Force is given.

.EXAMPLE
    ./infra/teardown.ps1
#>
[CmdletBinding()]
param(
    [ValidatePattern('^[a-z0-9]{1,8}$')]
    [string] $Environment = 'dev',

    [string] $ResourceGroup,

    [switch] $Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not $ResourceGroup) {
    $ResourceGroup = "rg-vog-$Environment"
}

$exists = az group exists --name $ResourceGroup
if ($exists -ne 'true') {
    Write-Host "Resource group '$ResourceGroup' does not exist. Nothing to do."
    return
}

$accounts = az cognitiveservices account list --resource-group $ResourceGroup --query '[].{name:name, location:location}' --output json | ConvertFrom-Json

if (-not $Force) {
    $answer = Read-Host "Delete resource group '$ResourceGroup' and everything in it? Type its name to confirm"
    if ($answer -ne $ResourceGroup) {
        Write-Host 'Cancelled.'
        return
    }
}

Write-Host "Deleting $ResourceGroup (this can take several minutes)"
az group delete --name $ResourceGroup --yes
if ($LASTEXITCODE -ne 0) {
    throw "Deleting $ResourceGroup failed."
}

foreach ($a in @($accounts)) {
    Write-Host "Purging soft-deleted account $($a.name)"
    az cognitiveservices account purge --location $a.location --resource-group $ResourceGroup --name $a.name
}

$envFile = Join-Path (Split-Path $PSScriptRoot -Parent) '.env.local'
if (Test-Path $envFile) {
    Remove-Item $envFile
}

Write-Host 'Done.'
