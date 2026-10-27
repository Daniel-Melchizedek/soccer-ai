#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$ProjectEndpoint = $env:VOG_FOUNDRY_PROJECT_ENDPOINT,
    [string]$JudgeDeployment = 'gpt-4.1',
    [string]$SourceDirectory = (Join-Path $PSScriptRoot '../foundry/evaluations/2026-10-27'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "../foundry/evaluations/runs/$(Get-Date -Format 'yyyyMMdd-HHmmss-fff')"),
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
    $arguments = @('rest', '--method', $Method, '--url', "$ProjectEndpoint/$Path",
        '--resource', 'https://ai.azure.com', '--output', 'json',
        '--output-file', (Join-Path $OutputDirectory $Artifact))
    if ($Body) { $arguments += @('--body', "@$(Join-Path $OutputDirectory $Body)") }
    & az @arguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Foundry $Method $Path failed. Saved successful steps remain in $OutputDirectory. Do not blindly resubmit an interrupted POST." }
    Get-Content (Join-Path $OutputDirectory $Artifact) -Raw -Encoding UTF8 | ConvertFrom-Json
}

$locales = @('en-gb', 'es-es', 'ta-in')
if ($CollectOnly) {
    $context = Get-Content (Join-Path $OutputDirectory 'context.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($ProjectEndpoint -and $ProjectEndpoint.TrimEnd('/') -ne $context.project_endpoint) {
        throw 'The requested project differs from the saved run context.'
    }
    $ProjectEndpoint = $context.project_endpoint
    $JudgeDeployment = $context.judge
}
else {
    if (-not $ProjectEndpoint -or -not $ProjectEndpoint.StartsWith('https://')) { throw 'Provide the deployed HTTPS Foundry ProjectEndpoint.' }
    $ProjectEndpoint = $ProjectEndpoint.TrimEnd('/')
    & (Join-Path $PSScriptRoot 'prepare_judge_evaluations.ps1') -SourceDirectory $SourceDirectory -OutputDirectory $OutputDirectory -JudgeDeployment $JudgeDeployment
    Write-Artifact 'context.json' @{ project_endpoint = $ProjectEndpoint; judge = $JudgeDeployment; created_at = [DateTimeOffset]::UtcNow.ToString('o') }
    foreach ($locale in $locales) {
        $runFile = "$locale-run-request.json"
        $evaluationFile = "$locale-evaluation-request.json"
        $run = Get-Content (Join-Path $OutputDirectory $runFile) -Raw -Encoding UTF8 | ConvertFrom-Json
        $evaluation = Get-Content (Join-Path $OutputDirectory $evaluationFile) -Raw -Encoding UTF8 | ConvertFrom-Json
        $agentName = $run.data_source.target.name
        $agent = Invoke-Project 'get' "agents/${agentName}?api-version=2025-11-15-preview" "$locale-agent.json"
        $version = [string]$agent.versions.latest.version
        if (-not $version) { throw "No deployed version found for $agentName." }
        $run.data_source.target.version = $version
        $run.name = "$agentName-v$version-judge-$JudgeDeployment-$(Get-Date -Format 'yyyyMMddHHmmss')"
        $evaluation.name = $run.name
        $evaluation.metadata.agent_version = $version
        $evaluation.metadata.azd_agent_version = $version
        $evaluatorName = $evaluation.testing_criteria[0].evaluator_name
        if ($locale -eq 'en-gb') {
            $original = Get-Content (Join-Path $SourceDirectory 'smoke-evaluator-v1.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            $rubric = @{
                display_name = $evaluatorName
                description = 'British English live-match smoke rubric.'
                evaluator_type = $original.evaluator_type
                categories = $original.categories
                supported_evaluation_levels = $original.supported_evaluation_levels
                definition = $original.definition
            }
        }
        else {
            $rubric = Get-Content (Join-Path $SourceDirectory "$locale-evaluator-request.json") -Raw -Encoding UTF8 | ConvertFrom-Json
        }
        Write-Artifact "$locale-evaluator-request.json" $rubric
        $registered = Invoke-Project 'post' "evaluators/$evaluatorName/versions?api-version=2025-11-15-preview" "$locale-evaluator.json" "$locale-evaluator-request.json"
        $evaluation.testing_criteria[0].evaluator_version = [string]$registered.version
        Write-Artifact $evaluationFile $evaluation
        Write-Artifact $runFile $run
        $group = Invoke-Project 'post' 'openai/v1/evals' "$locale-evaluation.json" $evaluationFile
        $submitted = Invoke-Project 'post' "openai/v1/evals/$($group.id)/runs" "$locale-run.json" $runFile
        Write-Host "Submitted $locale agent v$version using $JudgeDeployment : $($submitted.id)"
    }
}

$results = foreach ($locale in $locales) {
    $groupFile = Join-Path $OutputDirectory "$locale-evaluation.json"
    $runFile = Join-Path $OutputDirectory "$locale-run.json"
    if (-not (Test-Path $groupFile) -or -not (Test-Path $runFile)) {
        throw "Missing $locale submission artifacts; inspect successful requests before submitting again."
    }
    $group = Get-Content $groupFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $previous = Get-Content $runFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $runPath = "openai/v1/evals/$($group.id)/runs/$($previous.id)"
    $run = Invoke-Project 'get' $runPath "$locale-run.json"
    if ($run.status -eq 'completed') {
        $items = @()
        $pageNumber = 0
        $cursor = ''
        do {
            $pageNumber++
            $page = Invoke-Project 'get' "$runPath/output_items?limit=100$cursor" "$locale-output-page-$pageNumber.json"
            $items += @($page.data)
            if ($page.has_more) {
                if (-not $page.last_id) { throw 'Output pagination returned no cursor.' }
                $cursor = '&after=' + [Uri]::EscapeDataString($page.last_id)
            }
        } while ($page.has_more)
        Write-Artifact "$locale-output-items.json" @{ data = $items; has_more = $false }
        if ($items.Count -ne $run.result_counts.total) { throw "Incomplete output for $locale." }
    }
    [ordered]@{
        locale = $locale
        agent = $run.data_source.target.name
        agent_version = $run.data_source.target.version
        evaluation_id = $group.id
        run_id = $run.id
        status = $run.status
        counts = $run.result_counts
        report_url = $run.report_url
    }
}
Write-Artifact 'summary.json' @{ project_endpoint = $ProjectEndpoint; judge = $JudgeDeployment; runs = @($results) }
$results | ForEach-Object { Write-Host "$($_.locale): $($_.status) $($_.counts | ConvertTo-Json -Compress)" }
Write-Host "Artifacts: $OutputDirectory"
if (@($results | Where-Object status -in @('failed', 'canceled', 'cancelled')).Count) { throw 'An evaluation did not complete successfully. See saved run details.' }
if (@($results | Where-Object status -ne 'completed').Count) {
    Write-Host "Runs are still processing. Collect later: ./scripts/evaluate_agents.ps1 -CollectOnly -OutputDirectory '$OutputDirectory'"
}