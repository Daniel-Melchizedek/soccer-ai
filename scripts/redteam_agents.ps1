#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$ProjectEndpoint = $env:VOG_FOUNDRY_PROJECT_ENDPOINT,
    [string]$JudgeDeployment = 'gpt-4.1',
    [ValidateSet('en-gb', 'es-es', 'ta-in')]
    [string[]]$Locales = @('en-gb', 'es-es', 'ta-in'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "../foundry/evaluations/redteam-runs/$(Get-Date -Format 'yyyyMMdd-HHmmss-fff')"),
    [switch]$CollectOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    $cliDirectory = Join-Path ${env:ProgramFiles} 'Microsoft SDKs/Azure/CLI2/wbin'
    if (Test-Path (Join-Path $cliDirectory 'az.cmd')) { $env:PATH = "$cliDirectory;$env:PATH" }
    else { throw 'Install Azure CLI and run az login first.' }
}

function Write-Artifact([string]$Name, [object]$Value) {
    $Value | ConvertTo-Json -Depth 100 | Set-Content (Join-Path $OutputDirectory $Name) -Encoding utf8NoBOM
}

function Invoke-Project([string]$Method, [string]$Path, [string]$Artifact, [string]$Body) {
    $pendingFile = Join-Path $OutputDirectory "$Artifact.pending"
    $arguments = @('rest', '--method', $Method, '--url', "$ProjectEndpoint/$Path",
        '--resource', 'https://ai.azure.com', '--output', 'json',
        '--output-file', $pendingFile)
    if ($Body) { $arguments += @('--body', "@$(Join-Path $OutputDirectory $Body)") }
    & az @arguments | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Foundry $Method $Path failed. Inspect artifacts in $OutputDirectory; an interrupted POST may have succeeded. Do not blindly resubmit."
    }
    $response = Get-Content $pendingFile -Raw -Encoding UTF8 | ConvertFrom-Json
    Move-Item $pendingFile (Join-Path $OutputDirectory $Artifact) -Force
    return $response
}

if ($CollectOnly) {
    $context = Get-Content (Join-Path $OutputDirectory 'context.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($ProjectEndpoint -and $ProjectEndpoint.TrimEnd('/') -ne $context.project_endpoint) {
        throw 'The requested project differs from the saved run context.'
    }
    $ProjectEndpoint = $context.project_endpoint
    $Locales = @($context.locales)
}
else {
    if (-not $ProjectEndpoint -or -not $ProjectEndpoint.StartsWith('https://')) {
        throw 'Provide the deployed HTTPS Foundry ProjectEndpoint.'
    }
    $ProjectEndpoint = $ProjectEndpoint.TrimEnd('/')
    if (Test-Path $OutputDirectory) {
        if (@(Get-ChildItem $OutputDirectory -Force).Count) { throw 'Use a new empty output directory, or -CollectOnly for saved runs.' }
    }
    New-Item -ItemType Directory $OutputDirectory -Force | Out-Null
    $Locales = @($Locales | Select-Object -Unique)
    Write-Artifact 'context.json' @{
        project_endpoint = $ProjectEndpoint; locales = $Locales; judge = $JudgeDeployment
        created_at = [DateTimeOffset]::UtcNow.ToString('o')
    }
    foreach ($locale in $Locales) {
        $agentName = "vog-match-companion-$locale"
        $agent = Invoke-Project 'get' "agents/${agentName}?api-version=2025-11-15-preview" "$locale-agent.json"
        $version = [string]$agent.versions.latest.version
        if (-not $version) { throw "No deployed version found for $agentName." }
        $name = "$agentName-v$version-redteam-$(Get-Date -Format 'yyyyMMddHHmmss')"
        Write-Artifact "$locale-evaluation-request.json" @{
            name = $name
            data_source_config = @{ type = 'azure_ai_source'; scenario = 'red_team' }
            metadata = @{
                agent_name = $agentName; agent_version = $version
                azd_agent_name = $agentName; azd_agent_version = $version; locale = $locale
            }
            testing_criteria = @(
                @{
                    type = 'azure_ai_evaluator'; name = 'Sensitive Data Leakage'
                    evaluator_name = 'builtin.sensitive_data_leakage'; evaluator_version = '1'
                },
                @{
                    type = 'azure_ai_evaluator'; name = 'Task Adherence'
                    evaluator_name = 'builtin.task_adherence'; evaluator_version = '1'
                    initialization_parameters = @{ deployment_name = $JudgeDeployment }
                }
            )
        }
        Write-Artifact "$locale-run-request.json" @{
            name = $name
            data_source = @{
                type = 'azure_ai_red_team'
                item_generation_params = @{
                    type = 'red_team'; attack_strategies = @('Base64'); num_turns = 1
                }
                target = @{ type = 'azure_ai_agent'; name = $agentName; version = $version }
            }
        }
        $group = Invoke-Project 'post' 'openai/v1/evals' "$locale-evaluation.json" "$locale-evaluation-request.json"
        $run = Invoke-Project 'post' "openai/v1/evals/$($group.id)/runs" "$locale-run.json" "$locale-run-request.json"
        Write-Host "Submitted $locale red team for agent v$version : $($run.id)"
    }
}

$incomplete = $false
$collectionErrors = [Collections.Generic.List[object]]::new()
$results = foreach ($locale in $Locales) {
    try {
        $groupFile = Join-Path $OutputDirectory "$locale-evaluation.json"
        $runFile = Join-Path $OutputDirectory "$locale-run.json"
        if (-not (Test-Path $groupFile) -or -not (Test-Path $runFile)) {
            Write-Warning "Missing $locale submission artifacts. Inspect cloud state before resubmitting."
            $incomplete = $true
            continue
        }
        $group = Get-Content $groupFile -Raw -Encoding UTF8 | ConvertFrom-Json
        $previous = Get-Content $runFile -Raw -Encoding UTF8 | ConvertFrom-Json
        $runPath = "openai/v1/evals/$($group.id)/runs/$($previous.id)"
        $run = Invoke-Project 'get' $runPath "$locale-run.json"
        if ($run.status -eq 'completed') {
            $items = @()
            $cursor = ''
            $pageNumber = 0
            do {
                $pageNumber++
                $page = Invoke-Project 'get' "$runPath/output_items?limit=100$cursor" "$locale-output-page-$pageNumber.json"
                $items += @($page.data)
                if ($page.has_more) {
                    if (-not $page.last_id) { throw 'Output pagination returned no cursor.' }
                    $cursor = '&after=' + [Uri]::EscapeDataString($page.last_id)
                }
            } while ($page.has_more)
            if ($items.Count -ne $run.result_counts.total) { throw "Incomplete red-team output for $locale." }
            Write-Artifact "$locale-output-items.json" @{ data = $items; has_more = $false }
        }
        [ordered]@{
            locale = $locale; agent = $run.data_source.target.name; agent_version = $run.data_source.target.version
            evaluation_id = $group.id; run_id = $run.id; status = $run.status
            counts = $run.result_counts; report_url = $run.report_url
        }
    }
    catch {
        $collectionErrors.Add(@{ locale = $locale; error = $_.Exception.Message })
        Write-Warning "Could not collect $locale. Other locales will still be collected."
    }
}
Write-Artifact 'summary.json' @{
    project_endpoint = $ProjectEndpoint; runs = @($results); incomplete_submissions = $incomplete
    collection_errors = @($collectionErrors.ToArray())
}
$results | ForEach-Object { Write-Host "$($_.locale): $($_.status) $($_.report_url)" }
Write-Host "Artifacts: $OutputDirectory"
if ($collectionErrors.Count) { throw 'Some red-team results could not be collected. See collection_errors in summary.json and retry -CollectOnly.' }
if ($incomplete) { throw 'Some submissions are missing; successfully submitted runs were collected without resubmitting.' }
if (@($results | Where-Object status -in @('failed', 'canceled', 'cancelled')).Count) {
    throw 'A red-team run did not complete successfully. See saved run details.'
}
if (@($results | Where-Object status -ne 'completed').Count) {
    Write-Host "Runs are processing. Collect later: ./scripts/redteam_agents.ps1 -CollectOnly -OutputDirectory '$OutputDirectory'"
}