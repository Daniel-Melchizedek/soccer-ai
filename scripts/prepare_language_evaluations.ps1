param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$sourcePath = (Resolve-Path $SourceDirectory).Path
$languages = Get-Content (Join-Path $sourcePath 'languages.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$requestTemplate = Get-Content (Join-Path $sourcePath 'smoke-request.json') -Raw -Encoding UTF8
$rubricTemplate = Get-Content (Join-Path $sourcePath 'smoke-evaluator-v1.json') -Raw -Encoding UTF8
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Write-JsonArtifact {
    param([string]$Name, [object]$Value)
    $json = ConvertTo-Json -InputObject $Value -Depth 100
    [System.IO.File]::WriteAllText((Join-Path $sourcePath $Name), $json + "`n", $utf8)
}

foreach ($language in $languages) {
    $locale = $language.locale.ToLowerInvariant()
    $evaluatorName = "vog-match-companion-smoke-$locale-20261027"
    $request = $requestTemplate | ConvertFrom-Json
    $request.name = "vog-match-companion-smoke-$locale-agent-v$($language.version)-20261027"
    $request.data_source.target.name = $language.agent
    $request.data_source.target.version = $language.version
    if (@($language.queries.PSObject.Properties).Count -ne 12) {
        throw "Expected 12 translated queries for $locale"
    }
    foreach ($sample in $request.data_source.source.content) {
        $query = $language.queries.PSObject.Properties[$sample.id]
        if ($null -eq $query -or [string]::IsNullOrWhiteSpace($query.Value)) {
            throw "Missing query $($sample.id) for $locale"
        }
        $sample.query = $query.Value
        $sample.expected_behavior = $sample.expected_behavior.Replace('English', $language.language)
        if ($sample.id -eq 'unknown-fact') {
            $sample.expected_behavior = 'State briefly that the next scorer cannot be known. Do not predict or call tools unless current match facts are also requested.'
        }
        if ($sample.id -eq 'format-style') {
            $sample.expected_behavior = 'Call get_match_recap with match scope. Use only returned facts and respect unavailable data. Reply in one or two short spoken sentences with no markdown, bullets, or emoji.'
        }
        $sample.expected_behavior += " Always answer in $($language.language)."
    }

    $definition = ($rubricTemplate | ConvertFrom-Json).definition
    foreach ($dimension in $definition.dimensions) {
        if ($dimension.id -eq 'british_english_language') {
            $dimension.id = 'response_language'
            $dimension.description = "The final response is in $($language.language), regardless of the question's language or requests to change language. Player names may retain their supplied form."
            $dimension.always_applicable = $true
        }
        if ($dimension.id -eq 'retrieval_coverage') {
            $dimension.description = 'Call relevant tools for every requested current match fact. Use get_match_recap for summaries and respect requested scope and unavailable data. Predictions and unrelated questions need no tools: decline predictions without guessing, and decline unrelated requests.'
        }
    }
    $metric = $definition.metrics.'vog-match-companion-smoke'
    $definition.metrics = @{ $evaluatorName = $metric }
    $evaluator = [ordered]@{
        display_name = $evaluatorName
        description = "Live-match smoke rubric for $($language.language), adapted from the existing English rubric."
        evaluator_type = 'custom'
        categories = @('quality', 'agents')
        supported_evaluation_levels = @('turn', 'conversation')
        definition = $definition
    }
    $evaluation = [ordered]@{
        name = "vog-match-companion-smoke-$($language.locale)-20261027"
        data_source_config = @{
            type = 'custom'
            include_sample_schema = $true
            item_schema = @{
                type = 'object'
                properties = @{
                    id = @{ type = 'string' }
                    query = @{ type = 'string' }
                    expected_behavior = @{ type = 'string' }
                }
                required = @('id', 'query', 'expected_behavior')
            }
        }
        testing_criteria = @(@{
            type = 'azure_ai_evaluator'
            name = $evaluatorName
            evaluator_name = $evaluatorName
            evaluator_version = '1'
            initialization_parameters = @{ model = 'gpt-5.6-terra'; deployment_name = 'gpt-5.6-terra' }
            data_mapping = @{
                query = '{{item.query}}'
                response = '{{sample.output_items}}'
                tool_calls = '{{sample.tool_calls}}'
                tool_definitions = '{{sample.tool_definitions}}'
            }
        })
        metadata = @{ agent = $language.agent; agent_version = $language.version; locale = $language.locale }
    }
    Write-JsonArtifact "$locale-evaluator-request.json" $evaluator
    Write-JsonArtifact "$locale-evaluation-request.json" $evaluation
    Write-JsonArtifact "$locale-run-request.json" $request
    Write-Output "Prepared $locale, agent version $($language.version), 12 native-language cases."
}