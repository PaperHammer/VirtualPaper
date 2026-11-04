$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/InstallerTestSupport.ps1"

# Contract tests use callbacks only: no installer, GUI, registry or system configuration is touched.
foreach ($failure in @('', 'Install', 'Open', 'Uninstall')) {
    $calls = [Collections.Generic.List[string]]::new()
    $report = Invoke-InstallerLifecycle -Install {
        $calls.Add('Install')
        if ($failure -eq 'Install') { throw 'Expected install failure' }
    } -Open {
        $calls.Add('Open')
        if ($failure -eq 'Open') { throw 'Expected open failure' }
    } -Uninstall {
        $calls.Add('Uninstall')
        if ($failure -eq 'Uninstall') { throw 'Expected uninstall failure' }
    }
    $expected = if ($failure -eq 'Install') { 'Install,Uninstall' } else { 'Install,Open,Uninstall' }
    if (($calls -join ',') -cne $expected) { throw "Incorrect failure/cleanup order: $failure" }
    if (($report.Checks.Name -join ',') -cne 'Install,Open,Uninstall') { throw 'Incomplete lifecycle report.' }
    $accepted = $true
    try { Assert-InstallerReportPassed $report } catch { $accepted = $false }
    if ($accepted -ne ($failure -eq '')) { throw "Incorrect installer gate for failure: $failure" }
}

$good = Invoke-InstallerLifecycle -Install {} -Open {} -Uninstall {}
foreach ($mutation in @('MissingStage', 'DuplicateStage', 'FalsePass', 'WrongSuite', 'WrongCount', 'SkippedStage')) {
    $report = $good | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    switch ($mutation) {
        MissingStage { $report.Checks = @($report.Checks | Select-Object -First 2) }
        DuplicateStage { $report.Checks[2].Name = 'Open' }
        FalsePass { $report.Checks[1].Passed = $false }
        WrongSuite { $report.Suite = 'Open' }
        WrongCount { $report.Total = 2 }
        SkippedStage { $report.Checks[1].Status = 'Skipped' }
    }
    $rejected = $false
    try { Assert-InstallerReportPassed $report } catch { $rejected = $true }
    if (-not $rejected) { throw "Malformed installer report was accepted: $mutation" }
}
Write-Host '[OK] Installer lifecycle success/failure ordering, unconditional cleanup and report acceptance contracts.'

$headless = Resolve-UIOperationEnvironment ([pscustomobject]@{ Windows = $true; UserInteractive = $false })
$calls = [Collections.Generic.List[string]]::new()
$report = Invoke-InstallerLifecycle -Install { $calls.Add('Install') } -Open { throw 'Headless Open must not execute.' } -Uninstall { $calls.Add('Uninstall') } -Environment $headless
if (($calls -join ',') -cne 'Install,Uninstall' -or (Get-InstallerReportOutcome $report) -cne 'OpenSkipped') {
    throw 'Headless acceptance must install and uninstall, explicitly skipping window acceptance.'
}
foreach ($mutation in @('MissingEnvironment', 'FalseEnvironment', 'InstallSkipped', 'UninstallSkipped', 'BadCount', 'Failure')) {
    $bad = $report | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    switch ($mutation) {
        MissingEnvironment { $bad.Environment = $null }
        FalseEnvironment { $bad.Environment.Checks.Windows = $false }
        InstallSkipped { $bad.Checks[0].Status = 'Skipped' }
        UninstallSkipped { $bad.Checks[2].Status = 'Skipped' }
        BadCount { $bad.Skipped = 0 }
        Failure { $bad.Failed = 1 }
    }
    $accepted = $true
    try { Assert-InstallerReportPassed $bad } catch { $accepted = $false }
    if ($accepted) { throw "Invalid headless report accepted: $mutation" }
}
Write-Host '[OK] Headless install/uninstall, explicit Open skip and forged-skip rejection.'

foreach ($failure in @('', 'Install', 'Open', 'UIOperation', 'Uninstall')) {
    $calls = [Collections.Generic.List[string]]::new()
    $report = Invoke-InstallerLifecycle -Install {
        $calls.Add('Install'); if ($failure -eq 'Install') { throw 'Expected install failure' }
    } -Open {
        $calls.Add('Open'); if ($failure -eq 'Open') { throw 'Expected startup failure' }
    } -UIOperation {
        $calls.Add('UIOperation'); if ($failure -eq 'UIOperation') { throw 'Expected UI failure' }
    } -Uninstall {
        $calls.Add('Uninstall'); if ($failure -eq 'Uninstall') { throw 'Expected uninstall failure' }
    }
    $expected = switch ($failure) {
        Install { 'Install,Uninstall' }
        Open { 'Install,Open,Uninstall' }
        default { 'Install,Open,UIOperation,Uninstall' }
    }
    if (($calls -join ',') -cne $expected -or $report.UIOperationRequested -cne $true -or $report.Total -ne 4) {
        throw "Incorrect installed UI lifecycle: $failure"
    }
    $accepted = $true
    try { Assert-InstallerReportPassed $report } catch { $accepted = $false }
    if ($accepted -ne ($failure -eq '')) { throw "Incorrect installed UI result: $failure" }
}
$report = Invoke-InstallerLifecycle -Install {} -Open { throw 'No desktop' } -UIOperation { throw 'No desktop' } -Uninstall {} -Environment $headless
if ((Get-InstallerReportOutcome $report) -cne 'OpenSkipped' -or $report.Skipped -ne 2 -or
    ($report.Checks.Name -join ',') -cne 'Install,Open,UIOperation,Uninstall') {
    throw 'Headless installed UI must explicitly skip both Open and UIOperation.'
}
$report.Checks = @($report.Checks | Where-Object Name -NE 'UIOperation')
$report.Total = 3; $report.Skipped = 1
$accepted = $true
try { Assert-InstallerReportPassed $report } catch { $accepted = $false }
if ($accepted) { throw 'Requested UI stage must not be omitted from acceptance.' }
Write-Host '[OK] Installed UI ordering, prerequisite failure gates, UI-failure cleanup and mandatory UI stage.'
