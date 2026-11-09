#Requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string] $Root)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/TestReportSupport.ps1"
. "$PSScriptRoot/UIOperationTestSupport.ps1"
. "$PSScriptRoot/MsixTestSupport.ps1"
$Root = (Resolve-Path -LiteralPath $Root).Path
$report = Get-Content -LiteralPath (Join-Path $Root 'msix-acceptance.json') -Raw | ConvertFrom-Json
Assert-MsixAcceptanceReport $report
$projects = @(Get-ProjectTestReports -Root $Root -RequireCompleteExecution)
$coverage = @(Get-TestCoverageRows -Root $Root)
$uiCount = 0
foreach ($stage in @('first-run', 'extended-ui', 'retained-profile', 'fresh-profile')) {
    $ui = Assert-MsixUIReport (Join-Path $Root "$stage/ui-operation-results.json") $report.PackageFullName -Extended:($stage -eq 'extended-ui')
    $uiCount += @($ui.Results).Count
}
foreach ($property in @('InitialDeployment', 'MissingFolderRestored', 'ExistingDataPreserved', 'SavedDirectoryHonored', 'UninstallDataPreserved')) {
    if ($report.BuiltInWallpapers.$property -ne $true) { throw "Missing MSIX built-in wallpaper evidence: $property" }
}
$lines = @('# MSIX acceptance', '', "Package: $($report.PackageFullName)", '', "SHA256: $($report.PackageSha256)", '',
    "Project tests passed: $(($projects | Measure-Object Passed -Sum).Sum); UI cases passed: $uiCount.", '',
    '| Stage | Result |', '| --- | --- |')
$lines += @($report.Stages | ForEach-Object { "| $($_.Name) | $($_.Status) |" })
$lines += @('', '| Project | Covered lines | Valid lines |', '| --- | --- | --- |')
$lines += @($coverage | ForEach-Object { "| $($_.Project) | $($_.Covered) | $($_.Valid) |" })
$lines += @('', 'WACK, cross-version upgrades, clean-machine prerequisites and persistent OS settings are separate release checks.')
$lines | Set-Content -LiteralPath (Join-Path $Root 'msix-summary.md') -Encoding utf8
$lines | Write-Output
