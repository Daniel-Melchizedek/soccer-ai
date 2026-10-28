#requires -Version 7.0
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$sandbox = Join-Path ([IO.Path]::GetTempPath()) "vog-provision-test-$([Guid]::NewGuid().ToString('n'))"
New-Item -ItemType Directory $sandbox | Out-Null

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-Throws([scriptblock]$Action, [string]$ExpectedMessage) {
    try { & $Action }
    catch {
        Assert-True ($_.Exception.Message -like "*$ExpectedMessage*") "Unexpected failure: $($_.Exception.Message)"
        return
    }
    throw "Expected failure: $ExpectedMessage"
}

function Read-Argument([object[]]$Arguments, [string]$Name) {
    $index = [Array]::IndexOf($Arguments, $Name)
    if ($index -lt 0) { return '' }
    return [string]$Arguments[$index + 1]
}

function global:az {
    $arguments = @($args)
    $global:LASTEXITCODE = 0
    $command = $arguments -join ' '
    $state = $global:VogProvisionTestState
    $state.Calls.Add($command)
    if ($arguments[0] -eq 'rest') {
        $url = Read-Argument $arguments '--url'
        $method = Read-Argument $arguments '--method'
        Assert-True ((Read-Argument $arguments '--resource') -eq 'https://ai.azure.com') 'Wrong Foundry token audience.'
        $bodyFile = Read-Argument $arguments '--body'
        $body = if ($bodyFile) { Get-Content $bodyFile.Substring(1) -Raw | ConvertFrom-Json } else { $null }
        if ($url -match '/agents/') {
            $response = @{ versions = @{ latest = @{ version = '1' } } }
        }
        elseif ($url -match '/evaluators/') {
            $response = @{ version = '6' }
        }
        elseif ($url -match '/output_items') {
            $lastPage = $url.Contains('after=case-6')
            $numbers = if ($lastPage) { 7..12 } else { 1..6 }
            $response = @{
                data = @($numbers | ForEach-Object { @{ id = "case-$_"; results = @(@{ passed = $true; score = 0.8 }) } })
                has_more = -not $lastPage
                last_id = if ($lastPage) { 'case-12' } else { 'case-6' }
            }
        }
        elseif ($url -match '/runs$' -and $method -eq 'post') {
            Assert-True ($body.data_source.target.version -eq '1') 'Historical agent version was reused.'
            if ($body.data_source.type -eq 'azure_ai_red_team') {
                Assert-True ($body.data_source.item_generation_params.type -eq 'red_team') 'Wrong red-team generation type.'
                Assert-True ($body.data_source.item_generation_params.num_turns -eq 1) 'Unexpected red-team turn budget.'
                Assert-True ($body.data_source.item_generation_params.attack_strategies[0] -eq 'Base64') 'Wrong managed attack strategy.'
            }
            else { Assert-True ($body.data_source.source.content.Count -eq 12) 'Missing evaluation cases.' }
            $runId = "run-$($body.data_source.type)-$($body.data_source.target.name)"
            $response = @{
                id = $runId; data_source = $body.data_source; status = 'in_progress'
                result_counts = @{ total = 0; passed = 0; failed = 0 }; report_url = 'https://example.test/report'
            }
            $state.Runs[$runId] = $response
        }
        elseif ($url -match '/runs/([^/?]+)$') {
            if ($state.ContainsKey('FailedRun') -and $Matches[1] -eq $state.FailedRun) {
                $global:LASTEXITCODE = 1
                return
            }
            $response = $state.Runs[$Matches[1]]
            if ($state.Complete) {
                $response.status = 'completed'
                $response.result_counts = @{ total = 12; passed = 12; failed = 0 }
            }
        }
        elseif ($url -match '/evals$' -and $method -eq 'post') {
            if ($body.data_source_config.PSObject.Properties.Name -contains 'scenario' -and $body.data_source_config.scenario -eq 'red_team') {
                Assert-True ($body.testing_criteria[0].evaluator_name -eq 'builtin.sensitive_data_leakage') 'Missing red-team evaluator.'
                Assert-True ($body.testing_criteria[1].initialization_parameters.deployment_name -eq 'gpt-4.1') 'Wrong red-team judge.'
                Assert-True ($body.metadata.agent_version -eq '1') 'Red-team metadata used stale agent version.'
            }
            else {
                Assert-True ($body.testing_criteria[0].initialization_parameters.model -eq 'gpt-4.1') 'Wrong judge model.'
                Assert-True ($body.testing_criteria[0].initialization_parameters.deployment_name -eq 'gpt-4.1') 'Wrong judge deployment.'
                Assert-True ($body.testing_criteria[0].evaluator_version -eq '6') 'Historical rubric version was reused.'
            }
            $response = @{ id = "eval-$($body.metadata.locale)" }
        }
        else { throw "Unexpected REST request: $method $url" }
        $response | ConvertTo-Json -Depth 100 | Set-Content (Read-Argument $arguments '--output-file') -Encoding utf8NoBOM
        return
    }
    switch -Wildcard ($command) {
        'account show *' { return '{"name":"Test","user":"test-user"}' }
        'ad signed-in-user show *' { return 'test-principal' }
        'group create *' { return '/subscriptions/test/resourceGroups/rg-vog-test' }
        'keyvault list-deleted *' {
            if ($state.Scenario -eq 'Deleted') {
                return '[{"name":"kv-vog-test-old","originalId":"/subscriptions/test/resourceGroups/rg-vog-test/providers/Microsoft.KeyVault/vaults/kv-vog-test-old"},{"name":"kv-vog-test-other","originalId":"/subscriptions/test/resourceGroups/other/providers/Microsoft.KeyVault/vaults/kv-vog-test-other"}]'
            }
            return '[]'
        }
        'cognitiveservices account list-deleted *' {
            if ($state.Scenario -eq 'Deleted') {
                return '[{"name":"vog-test-old","location":"swedencentral","id":"/subscriptions/test/providers/Microsoft.CognitiveServices/locations/swedencentral/resourceGroups/rg-vog-test/deletedAccounts/vog-test-old"},{"name":"vog-test-other","location":"swedencentral","id":"/subscriptions/test/providers/Microsoft.CognitiveServices/locations/swedencentral/resourceGroups/other/deletedAccounts/vog-test-other"}]'
            }
            return '[]'
        }
        'keyvault recover *' { return }
        'cognitiveservices account recover *' { return }
        'keyvault list *' { if ($state.Scenario -ne 'Fresh') { return 'kv-vog-test-old' }; return '' }
        'containerapp list *' { if ($state.Scenario -in @('Existing', 'UnreadableKey')) { return 'registry.test/gateway:existing' }; return '' }
        'keyvault secret show *' {
            if ($state.Scenario -eq 'UnreadableKey') { $global:LASTEXITCODE = 1; return }
            return 'test-existing-key'
        }
        'deployment group create *' {
            $parameters = Get-Content (Read-Argument $arguments '--parameters').Substring(1) -Raw | ConvertFrom-Json
            Assert-True ($parameters.parameters.evaluationModelName.value -eq 'gpt-4.1') 'Judge missing from infrastructure parameters.'
            if ($state.Scenario -eq 'Existing') { Assert-True ($parameters.parameters.mcpApiKey.value -eq 'test-existing-key') 'Existing MCP key was rotated.' }
            $values = @{
                VOG_REGISTRY_NAME = 'registry'; VOG_STORAGE_ACCOUNT_NAME = 'storage'; VOG_GATEWAY_URL = 'https://gateway.example.test'
                VOG_FOUNDRY_PROJECT_ENDPOINT = 'https://foundry.example.test/api/projects/test'; VOG_EVALUATION_MODEL_DEPLOYMENT = 'gpt-4.1'
                VOG_AGENT_MODEL_DEPLOYMENT = 'gpt-5.6-terra'; VOG_AGENT_GUARDRAIL_ID = 'guardrail'; VOG_MCP_URL = 'https://gateway.example.test/mcp'
                VOG_MCP_CONNECTION_PREFIX = 'vog-match-mcp'
            }
            $outputs = @{}
            foreach ($entry in $values.GetEnumerator()) { $outputs[$entry.Key] = @{ value = $entry.Value } }
            return $outputs | ConvertTo-Json -Depth 10
        }
        'acr build *' { return }
        'acr show *' { return 'registry.example.test' }
        'storage blob upload *' {
            $data = Get-Content (Read-Argument $arguments '--file') -Raw | ConvertFrom-Json
            Assert-True ($data.Count -eq 16) 'Invalid historical seed.'
            return
        }
        default { throw "Unexpected Azure command: $command" }
    }
}

function global:git { $global:LASTEXITCODE = 0; return 'testsha' }
function global:dotnet {
    $global:LASTEXITCODE = 0
    Assert-True ($PWD.Path -eq $global:VogProvisionTestState.Root) 'Agent setup did not run at repository root.'
    Assert-True ($env:VOG_FOUNDRY_PROJECT_ENDPOINT -eq 'https://foundry.example.test/api/projects/test') 'Agent setup received stale configuration.'
}
function global:Invoke-WebRequest { return @{ StatusCode = 200 } }

try {
    foreach ($directory in @('infra', 'scripts', 'data')) { Copy-Item (Join-Path $repoRoot $directory) $sandbox -Recurse }
    $inputs = Join-Path $sandbox 'foundry/evaluations/2026-10-27'
    New-Item -ItemType Directory $inputs -Force | Out-Null
    foreach ($file in @('smoke-request.json', 'smoke-evaluation.json', 'smoke-evaluator-v1.json', 'es-es-evaluation-request.json', 'es-es-run-request.json', 'es-es-evaluator-request.json', 'ta-in-evaluation-request.json', 'ta-in-run-request.json', 'ta-in-evaluator-request.json')) {
        Copy-Item (Join-Path $repoRoot "foundry/evaluations/2026-10-27/$file") $inputs
    }
    foreach ($scenario in @('Fresh', 'Deleted', 'Existing', 'UnreadableKey')) {
        $global:VogProvisionTestState = @{ Scenario = $scenario; Root = $sandbox; Complete = $false; Runs = @{}; Calls = [Collections.Generic.List[string]]::new() }
        $output = Join-Path $sandbox "results-$scenario"
        $redTeamOutput = Join-Path $sandbox "redteam-$scenario"
        $failed = $false
        try { & (Join-Path $sandbox 'infra/provision.ps1') -Environment test -EvaluationOutputDirectory $output -RedTeamOutputDirectory $redTeamOutput }
        catch { if ($scenario -ne 'UnreadableKey') { throw }; $failed = $true }
        $calls = $global:VogProvisionTestState.Calls
        if ($scenario -eq 'UnreadableKey') {
            Assert-True $failed 'Unreadable existing key did not stop provisioning.'
            Assert-True (@($calls | Where-Object { $_ -like 'deployment group create *' }).Count -eq 0) 'Infrastructure changed after key-read failure.'
        }
        else {
            Assert-True (@($calls | Where-Object { $_ -like 'storage blob upload *' }).Count -eq 2) 'Both historical datasets were not seeded.'
            Assert-True ($global:VogProvisionTestState.Runs.Count -eq 6) 'All three quality and three red-team runs were not submitted.'
            $global:VogProvisionTestState.Complete = $true
            & (Join-Path $sandbox 'scripts/evaluate_agents.ps1') -CollectOnly -OutputDirectory $output
            $postsBefore = @($calls | Where-Object { $_ -like '*--method post*' }).Count
            & (Join-Path $sandbox 'scripts/redteam_agents.ps1') -CollectOnly -OutputDirectory $redTeamOutput
            Assert-True (@($calls | Where-Object { $_ -like '*--method post*' }).Count -eq $postsBefore) 'CollectOnly resubmitted red-team scans.'
            foreach ($locale in @('en-gb', 'es-es', 'ta-in')) {
                $items = Get-Content (Join-Path $output "$locale-output-items.json") -Raw | ConvertFrom-Json
                Assert-True ($items.data.Count -eq 12 -and -not $items.has_more) 'Paginated evaluation results were incomplete.'
                $redTeamItems = Get-Content (Join-Path $redTeamOutput "$locale-output-items.json") -Raw | ConvertFrom-Json
                Assert-True ($redTeamItems.data.Count -eq 12 -and -not $redTeamItems.has_more) 'Paginated red-team results were incomplete.'
            }
            $summary = Get-Content (Join-Path $output 'summary.json') -Raw | ConvertFrom-Json
            Assert-True (@($summary.runs | Where-Object status -eq 'completed').Count -eq 3) 'Summary did not reflect completion.'
        }
        if ($scenario -eq 'Deleted') {
            Assert-True (@($calls | Where-Object { $_ -like '* recover *' }).Count -eq 2) 'Recovery was not scoped to the deleted environment.'
            Assert-True (@($calls | Where-Object { $_ -like '* purge *' }).Count -eq 0) 'Provisioning must not purge resources.'
        }
        Write-Host "PASS: $scenario provisioning and evaluation workflow."
    }

    foreach ($skip in @('SkipRedTeam', 'SkipEvaluations')) {
        $global:VogProvisionTestState = @{ Scenario = 'Existing'; Root = $sandbox; Complete = $false; Runs = @{}; Calls = [Collections.Generic.List[string]]::new() }
        $parameters = @{ Environment = 'test'; EvaluationOutputDirectory = (Join-Path $sandbox $skip); $skip = $true }
        & (Join-Path $sandbox 'infra/provision.ps1') @parameters
        $expectedRuns = if ($skip -eq 'SkipRedTeam') { 3 } else { 0 }
        Assert-True ($global:VogProvisionTestState.Runs.Count -eq $expectedRuns) "$skip submitted unexpected runs."
        Write-Host "PASS: $skip avoids red-team submissions."
    }

    $redTeamScript = Join-Path $sandbox 'scripts/redteam_agents.ps1'
    $redTeamOutput = Join-Path $sandbox 'redteam-recovery'
    & $redTeamScript -OutputDirectory $redTeamOutput
    $calls = $global:VogProvisionTestState.Calls
    $postsBefore = @($calls | Where-Object { $_ -like '*--method post*' }).Count
    Assert-Throws { & $redTeamScript -OutputDirectory $redTeamOutput } 'new empty output directory'
    Assert-Throws { & $redTeamScript -CollectOnly -OutputDirectory $redTeamOutput -ProjectEndpoint 'https://other.example.test/project' } 'differs from the saved run context'
    $callsBefore = $calls.Count
    Assert-Throws { & (Join-Path $sandbox 'infra/provision.ps1') -RedTeamOutputDirectory $redTeamOutput } 'new empty RedTeamOutputDirectory'
    Assert-True ($calls.Count -eq $callsBefore) 'Provisioning changed Azure before rejecting a reused red-team output directory.'

    $global:VogProvisionTestState.FailedRun = 'run-azure_ai_red_team-vog-match-companion-en-gb'
    $savedRun = Get-Content (Join-Path $redTeamOutput 'en-gb-run.json') -Raw
    Assert-Throws { & $redTeamScript -CollectOnly -OutputDirectory $redTeamOutput } 'Some red-team results could not be collected'
    $summary = Get-Content (Join-Path $redTeamOutput 'summary.json') -Raw | ConvertFrom-Json
    Assert-True ($summary.runs.Count -eq 2 -and $summary.collection_errors.Count -eq 1) 'GET failure prevented collection of later locales.'
    Assert-True ((Get-Content (Join-Path $redTeamOutput 'en-gb-run.json') -Raw) -eq $savedRun) 'Failed GET overwrote the saved run ID.'
    $global:VogProvisionTestState.Remove('FailedRun')

    foreach ($status in @('failed', 'canceled')) {
        $global:VogProvisionTestState.Runs['run-azure_ai_red_team-vog-match-companion-en-gb'].status = $status
        Assert-Throws { & $redTeamScript -CollectOnly -OutputDirectory $redTeamOutput } 'did not complete successfully'
    }
    $global:VogProvisionTestState.Runs['run-azure_ai_red_team-vog-match-companion-en-gb'].status = 'in_progress'
    Remove-Item (Join-Path $redTeamOutput 'en-gb-run.json')
    Assert-Throws { & $redTeamScript -CollectOnly -OutputDirectory $redTeamOutput } 'Some submissions are missing'
    $summary = Get-Content (Join-Path $redTeamOutput 'summary.json') -Raw | ConvertFrom-Json
    Assert-True ($summary.incomplete_submissions -and $summary.runs.Count -eq 2) 'Partial submission collection lost successful runs.'
    Assert-True (@($calls | Where-Object { $_ -like '*--method post*' }).Count -eq $postsBefore) 'Failure recovery made new POST requests.'
    Write-Host 'PASS: red-team duplicate prevention, context validation, terminal failures and partial collection.'
}
finally {
    foreach ($name in @('az', 'git', 'dotnet', 'Invoke-WebRequest')) { Remove-Item "Function:/$name" }
    Remove-Variable VogProvisionTestState -Scope Global -ErrorAction SilentlyContinue
    Remove-Item $sandbox -Recurse -Force
}