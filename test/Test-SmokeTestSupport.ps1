$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/SmokeTestSupport.ps1"

# Contract tests use callbacks only: no installer, GUI, registry or system configuration is touched.
foreach ($failure in @('', 'Install', 'Launch', 'Uninstall')) {
    $calls = [Collections.Generic.List[string]]::new()
    $report = Invoke-SmokeLifecycle -Install {
        $calls.Add('Install')
        if ($failure -eq 'Install') { throw 'Expected install failure' }
    } -Launch {
        $calls.Add('Launch')
        if ($failure -eq 'Launch') { throw 'Expected launch failure' }
    } -Uninstall {
        $calls.Add('Uninstall')
        if ($failure -eq 'Uninstall') { throw 'Expected uninstall failure' }
    }
    $expected = if ($failure -eq 'Install') { 'Install,Uninstall' } else { 'Install,Launch,Uninstall' }
    if (($calls -join ',') -cne $expected) { throw "Incorrect failure/cleanup order: $failure" }
    if (($report.Checks.Name -join ',') -cne 'Install,Launch,Uninstall') { throw 'Incomplete lifecycle report.' }
    $accepted = $true
    try { Assert-SmokeReportPassed $report } catch { $accepted = $false }
    if ($accepted -ne ($failure -eq '')) { throw "Incorrect smoke gate for failure: $failure" }
}

$good = Invoke-SmokeLifecycle -Install {} -Launch {} -Uninstall {}
foreach ($mutation in @('MissingStage', 'DuplicateStage', 'FalsePass', 'WrongSuite', 'WrongCount', 'SkippedStage')) {
    $report = $good | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    switch ($mutation) {
        MissingStage { $report.Checks = @($report.Checks | Select-Object -First 2) }
        DuplicateStage { $report.Checks[2].Name = 'Launch' }
        FalsePass { $report.Checks[1].Passed = $false }
        WrongSuite { $report.Suite = 'Launch' }
        WrongCount { $report.Total = 2 }
        SkippedStage { $report.Checks[1].Status = 'Skipped' }
    }
    $rejected = $false
    try { Assert-SmokeReportPassed $report } catch { $rejected = $true }
    if (-not $rejected) { throw "Malformed smoke report was accepted: $mutation" }
}
Write-Host '[OK] Smoke lifecycle success/failure ordering, unconditional cleanup and report acceptance contracts.'
