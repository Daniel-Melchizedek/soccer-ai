param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDirectory,
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,
    [Parameter(Mandatory = $true)]
    [string]$JudgeDeployment
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$sourcePath = (Resolve-Path $SourceDirectory).Path
if (Test-Path $OutputDirectory) { throw 'Output directory must not already exist.' }
$sources = @(
    @{ Locale = 'en-gb'; Evaluation = 'smoke-evaluation.json'; Run = 'smoke-request.json' },
    @{ Locale = 'es-es'; Evaluation = 'es-es-evaluation-request.json'; Run = 'es-es-run-request.json' },
    @{ Locale = 'ta-in'; Evaluation = 'ta-in-evaluation-request.json'; Run = 'ta-in-run-request.json' }
)
$prepared = foreach ($source in $sources) {
    $original = Get-Content (Join-Path $sourcePath $source.Evaluation) -Raw -Encoding UTF8 | ConvertFrom-Json
    $run = Get-Content (Join-Path $sourcePath $source.Run) -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($run.data_source.source.content.Count -ne 12) { throw "Expected 12 cases for $($source.Locale)." }
    $criteria = @($original.testing_criteria | ForEach-Object {
        @{
            type = $_.type
            name = $_.name
            evaluator_name = $_.evaluator_name
            evaluator_version = '1'
            initialization_parameters = @{ model = $JudgeDeployment; deployment_name = $JudgeDeployment }
            data_mapping = $_.data_mapping
        }
    })
    $run.name = "$($run.name)-judge-$JudgeDeployment"
    $evaluation = @{
        name = "$($original.name)-judge-$JudgeDeployment"
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
        testing_criteria = $criteria
        metadata = @{
            agent = $run.data_source.target.name
            agent_version = $run.data_source.target.version
            azd_agent = $run.data_source.target.name
            azd_agent_version = $run.data_source.target.version
            locale = $source.Locale
            judge = $JudgeDeployment
        }
    }
    @{ Locale = $source.Locale; Evaluation = $evaluation; Run = $run }
}
$outputPath = (New-Item -ItemType Directory -Path $OutputDirectory).FullName
$utf8 = New-Object System.Text.UTF8Encoding($false)
foreach ($entry in $prepared) {
    foreach ($kind in @('Evaluation', 'Run')) {
        $file = Join-Path $outputPath "$($entry.Locale)-$($kind.ToLowerInvariant())-request.json"
        $json = ConvertTo-Json -InputObject $entry[$kind] -Depth 100
        [System.IO.File]::WriteAllText($file, $json + "`n", $utf8)
    }
    Write-Output "Prepared $($entry.Locale): judge $JudgeDeployment, agent version $($entry.Run.data_source.target.version), 12 cases."
}