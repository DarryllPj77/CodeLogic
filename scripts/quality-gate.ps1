[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
$resultsRoot = Join-Path $repositoryRoot "artifacts/quality/$runId"
$unitResults = Join-Path $resultsRoot 'unit'
$integrationResults = Join-Path $resultsRoot 'integration'

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string]$Label,

        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    Write-Host "`n== $Label ==" -ForegroundColor Cyan
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed with exit code $LASTEXITCODE."
    }
}

function Assert-Coverage {
    param(
        [Parameter(Mandatory)]
        [string]$ReportDirectory,

        [Parameter(Mandatory)]
        [string]$Module,

        [Parameter(Mandatory)]
        [double]$MinimumLineRate,

        [Parameter(Mandatory)]
        [double]$MinimumBranchRate
    )

    $report = Get-ChildItem $ReportDirectory -Recurse -Filter 'coverage.cobertura.xml' |
        Select-Object -First 1
    if ($null -eq $report) {
        throw "Coverage report was not produced under $ReportDirectory."
    }

    [xml]$coverage = Get-Content $report.FullName
    $package = $coverage.coverage.packages.package |
        Where-Object { $_.name -eq $Module } |
        Select-Object -First 1
    if ($null -eq $package) {
        throw "Coverage report does not contain module $Module."
    }

    $lineRate = [double]$package.'line-rate'
    $branchRate = [double]$package.'branch-rate'
    Write-Host ("{0}: line {1:P1}, branch {2:P1}" -f $Module, $lineRate, $branchRate)

    if ($lineRate -lt $MinimumLineRate -or $branchRate -lt $MinimumBranchRate) {
        throw ("{0} coverage is below its regression floor: line {1:P0}, branch {2:P0}." -f `
            $Module, $MinimumLineRate, $MinimumBranchRate)
    }
}

Push-Location $repositoryRoot
try {
    New-Item -ItemType Directory -Path $unitResults, $integrationResults -Force | Out-Null

    Invoke-DotNet 'Restore' @('restore', 'CodeLogic.slnx')
    Invoke-DotNet 'Formatting' @('format', 'CodeLogic.slnx', '--no-restore', '--verify-no-changes')
    Invoke-DotNet 'Build' @('build', 'CodeLogic.slnx', '--configuration', $Configuration, '--no-restore')
    Invoke-DotNet 'Unit tests and coverage' @(
        'test', 'tests/CodeLogic.UnitTests/CodeLogic.UnitTests.csproj',
        '--configuration', $Configuration, '--no-build', '--no-restore',
        '--settings', 'CodeLogic.runsettings', '--collect:XPlat Code Coverage',
        '--results-directory', $unitResults)
    Invoke-DotNet 'Integration tests and coverage' @(
        'test', 'tests/CodeLogic.IntegrationTests/CodeLogic.IntegrationTests.csproj',
        '--configuration', $Configuration, '--no-build', '--no-restore',
        '--settings', 'CodeLogic.runsettings', '--collect:XPlat Code Coverage',
        '--results-directory', $integrationResults)

    Write-Host "`n== Coverage regression floors ==" -ForegroundColor Cyan
    Assert-Coverage $unitResults 'CodeLogic.Domain' 0.75 0.70
    Assert-Coverage $integrationResults 'CodeLogic.Application' 0.75 0.65
    Assert-Coverage $integrationResults 'CodeLogic.Infrastructure' 0.85 0.60

    Invoke-DotNet 'EF migration model check' @(
        'ef', 'migrations', 'has-pending-model-changes',
        '--project', 'src/CodeLogic.Infrastructure/CodeLogic.Infrastructure.csproj',
        '--startup-project', 'src/CodeLogic.Web/CodeLogic.Web.csproj',
        '--context', 'CodeLogicDbContext', '--configuration', $Configuration, '--no-build')
    Invoke-DotNet 'Package vulnerability audit' @(
        'list', 'CodeLogic.slnx', 'package', '--vulnerable', '--include-transitive', '--no-restore')

    Write-Host "`nQuality gate passed. Reports: $resultsRoot" -ForegroundColor Green
}
finally {
    Pop-Location
}
