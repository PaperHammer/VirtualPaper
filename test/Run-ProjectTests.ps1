#requires -Version 7.2
param(
    [Parameter(Mandatory)][string]$JobId,
    [switch]$IncludeMLIntegration
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TestReportSupport.ps1')
$spec = Get-ProjectTestSpecs | Where-Object Id -CEQ $JobId
if ($null -eq $spec) { throw "Unknown test job: $JobId" }
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resultsDirectory = "TestResults/$($spec.Directory)"
$reportPath = Join-Path $repositoryRoot "$resultsDirectory/$($spec.Report)"
if (Test-Path -LiteralPath $reportPath) { throw "Refusing to reuse an old test report: $reportPath" }
if ($env:GITHUB_OUTPUT) {
    "artifact=$($spec.Artifact)`nresults_directory=$resultsDirectory" >> $env:GITHUB_OUTPUT
}
$arguments = @('test', "src/$($spec.Project)/$($spec.Project).csproj", '--configuration', 'Release',
    '--no-build', '--verbosity', 'normal', '--logger', "trx;LogFileName=$($spec.Report)", '--results-directory', $resultsDirectory)
if ($spec.Coverage) { $arguments += @('--settings', 'src/test.runsettings') }
if ($JobId -eq 'test-ml' -and -not $IncludeMLIntegration) { $arguments += @('--filter', 'TestCategory!=Integration') }
Push-Location -LiteralPath $repositoryRoot
$previousReleaseBin = $env:RELEASE_BIN_DIR
try {
    if ($JobId -eq 'test-artifacts') {
        $env:RELEASE_BIN_DIR = Join-Path $repositoryRoot 'src/VirtualPaper/bin/Release/net8.0-windows10.0.19041.0'
    }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "$JobId failed (exit $LASTEXITCODE)." }
    Assert-TrxReportPassed $reportPath | Format-Table Total, Executed, Passed
} finally {
    $env:RELEASE_BIN_DIR = $previousReleaseBin
    Pop-Location
}
