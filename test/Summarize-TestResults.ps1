#requires -Version 7.2
param([Parameter(Mandatory)][string]$Root)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TestReportSupport.ps1')
. (Join-Path $PSScriptRoot 'InstallerTestSupport.ps1')
. (Join-Path $PSScriptRoot 'UIOperationTestSupport.ps1')
$reports = @(Get-ProjectTestReports -Root $Root)
$installer = Get-Content -LiteralPath (Join-Path $Root 'test-results-installer/installer-results.json') -Raw | ConvertFrom-Json
$installerOutcome = Get-InstallerReportOutcome $installer
$ui = Get-Content -LiteralPath (Join-Path $Root 'test-results-ui-operation/ui-operation-results.json') -Raw | ConvertFrom-Json
$uiOutcome = Get-UIOperationReportOutcome $ui
$coverage = @(Get-TestCoverageRows -Root $Root)
$covered = ($coverage | Measure-Object Covered -Sum).Sum
$valid = ($coverage | Measure-Object Valid -Sum).Sum
$rate = if ($valid -eq 0) { 0 } else { [math]::Round(100 * $covered / $valid, 2) }
$installerSummary = if ($installerOutcome -eq 'OpenSkipped') { 'installed and uninstalled; **Open skipped** (no interactive desktop)' } else { 'passed' }
$summary = @('### Test results', '', "- Installer lifecycle: $installerSummary", "- Project reports: $($reports.Count) passed")
if ($uiOutcome -eq 'Skipped') {
    $summary += "- WinUI: **Skipped** [$($ui.SkipReason)]: $($ui.Environment.Reason)"
    $summary += '- No real UI acceptance was executed; this is not a passing UI result.'
} else {
    $summary += "- WinUI: passed ($($ui.Results.Count) cases)"
}
$summary += @('', '### Code coverage', '', '| Project | Lines | Coverage |', '| --- | ---: | ---: |')
foreach ($row in $coverage) {
    $projectRate = if ($row.Valid -eq 0) { 0 } else { [math]::Round(100 * $row.Covered / $row.Valid, 2) }
    $summary += "| $($row.Project) | $($row.Covered) / $($row.Valid) | $projectRate% |"
}
$summary += @('', "**Combined:** $covered / $valid lines covered (**$rate%**).")
if ($env:GITHUB_STEP_SUMMARY) { $summary -join "`n" >> $env:GITHUB_STEP_SUMMARY }
# Emit success only after every required report and coverage record has been validated.
if ($env:GITHUB_OUTPUT) {
    "ui_outcome=$uiOutcome" >> $env:GITHUB_OUTPUT
    "installer_outcome=$installerOutcome" >> $env:GITHUB_OUTPUT
}
$summary -join "`n" | Write-Host
