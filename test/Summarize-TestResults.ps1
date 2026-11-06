#requires -Version 7.2
param(
    [Parameter(Mandatory)][string]$Root,
    [switch]$RequireFullAcceptance,
    [switch]$AllowUIFailure,
    [ValidateSet('success', 'failure', 'cancelled', 'skipped')][string]$UIJobResult
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TestReportSupport.ps1')
. (Join-Path $PSScriptRoot 'InstallerTestSupport.ps1')
. (Join-Path $PSScriptRoot 'UIOperationTestSupport.ps1')
$reports = @(Get-ProjectTestReports -Root $Root -RequireCompleteExecution:$RequireFullAcceptance)
$installer = Get-Content -LiteralPath (Join-Path $Root 'test-results-installer/installer-results.json') -Raw | ConvertFrom-Json
$installerOutcome = Get-InstallerReportOutcome $installer
$ui = $null
try {
    $ui = Get-Content -LiteralPath (Join-Path $Root 'test-results-ui-operation/ui-operation-results.json') -Raw | ConvertFrom-Json
    $uiOutcome = Get-UIOperationReportOutcome $ui -AllowFailure:$AllowUIFailure
} catch {
    if (-not $AllowUIFailure) { throw }
    $uiOutcome = 'Failed'
}
# The independent UI job may fail after writing a passing report (for example,
# during cleanup), or before it can produce a report. Preserve that failure.
if ($UIJobResult -and $UIJobResult -ne 'success') { $uiOutcome = 'Failed' }
if (-not $AllowUIFailure -and ($uiOutcome -eq 'Failed' -or ($RequireFullAcceptance -and $uiOutcome -ne 'Passed'))) {
    throw 'UI acceptance did not pass; strict local/release validation rejects failures and required skips.'
}
$coverage = @(Get-TestCoverageRows -Root $Root)
$covered = ($coverage | Measure-Object Covered -Sum).Sum
$valid = ($coverage | Measure-Object Valid -Sum).Sum
$rate = if ($valid -eq 0) { 0 } else { [math]::Round(100 * $covered / $valid, 2) }
$summary = @('### Test results', '', '- Installer smoke: **Passed** (including startup, normal exit, reinstall and uninstall)', "- Project reports: $($reports.Count) passed")
if ($uiOutcome -eq 'Skipped') {
    $summary += "- WinUI: **Skipped** [$($ui.SkipReason)]: $($ui.Environment.Reason)"
    $summary += '- No real UI acceptance was executed; this is not a passing UI result.'
} elseif ($uiOutcome -eq 'Failed') {
    $summary += '- WinUI: **Failed (non-blocking)**. Required CI checks passed; full UI acceptance did not pass.'
    if ($UIJobResult) { $summary += "- UI job result: $UIJobResult" }
    foreach ($case in @($ui.Results | Where-Object { $_.Passed -ne $true })) {
        $summary += ('  - {0}: {1}' -f $case.Name, ([string]$case.Error -split '\r?\n')[0])
    }
    if ($null -eq $ui) { $summary += '- UI report is missing or unreadable; inspect the UI job logs and retained artifacts.' }
    Write-Warning 'Real UI acceptance failed (non-blocking). See the raw reports and artifacts.'
    if ($env:GITHUB_ACTIONS -eq 'true') { Write-Host '::warning::Real UI acceptance failed (non-blocking); this is not a passing UI result.' }
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
