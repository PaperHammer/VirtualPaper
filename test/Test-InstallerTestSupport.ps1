$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/InstallerTestSupport.ps1"

# Callbacks only: no installer, GUI, registry or system configuration is touched.
foreach ($withUI in @($false, $true)) {
    $stages = @('Install', 'Open', 'Reinstall', 'VerifyPreserved', 'OpenFresh')
    if ($withUI) { $stages += 'UIOperation' }
    $stages += 'Uninstall'
    foreach ($failure in (@('') + $stages)) {
        $calls = [Collections.Generic.List[string]]::new()
        $handlers = @{}
        foreach ($name in $stages) {
            $handlers[$name] = {
                $calls.Add($name)
                if ($failure -eq $name) { throw "Expected $name failure" }
            }.GetNewClosure()
        }
        $report = Invoke-InstallerLifecycle @handlers
        $expected = [Collections.Generic.List[string]]::new()
        foreach ($name in $stages) {
            if ($name -eq 'Uninstall' -or $failure -eq '' -or $expected -notcontains $failure) { $expected.Add($name) }
        }
        if (($calls -join ',') -cne ($expected -join ',') -or ($report.Checks.Name -join ',') -cne ($stages -join ',')) {
            throw "Incorrect failure/cleanup order: $failure, UI=$withUI"
        }
        $accepted = $true
        try { Assert-InstallerReportPassed $report } catch { $accepted = $false }
        if ($accepted -ne ($failure -eq '')) { throw "Incorrect result gate: $failure, UI=$withUI" }
        $accepted = $true
        try { $outcome = Get-InstallerReportOutcome $report -AllowUIFailure } catch { $accepted = $false }
        $uiOnly = $withUI -and $failure -eq 'UIOperation'
        if ($accepted -ne ($failure -eq '' -or $uiOnly) -or ($uiOnly -and $outcome -cne 'UIFailed')) {
            throw "Only the extended UI stage may be non-blocking: $failure, UI=$withUI"
        }
    }
}

$handlers = @{ Install = {}; Open = {}; Reinstall = {}; VerifyPreserved = {}; OpenFresh = {}; UIOperation = {}; Uninstall = {} }
$good = Invoke-InstallerLifecycle @handlers
# Exercise the production runner's final report/exit-code gate without any
# installation. A native UI host's stale exit code must not fail an opted-in CI.
$report = Invoke-InstallerLifecycle -Install {} -Open {} -Reinstall {} -VerifyPreserved {} -OpenFresh {} `
    -UIOperation { throw 'Expected native UI host failure' } -Uninstall {}
$runnerText = Get-Content -LiteralPath "$PSScriptRoot/Run-InstallerTests.ps1" -Raw
$finishAt = $runnerText.IndexOf('Assert-InstallerReportPassed $report ')
if ($finishAt -lt 0) { throw 'Installer runner result gate missing.' }
$finish = [scriptblock]::Create($runnerText.Substring($finishAt))
$previousExitCode = Get-Variable -Name LASTEXITCODE -Scope Global -ErrorAction SilentlyContinue
try {
    foreach ($AllowUIFailure in @($false, $true)) {
        $global:LASTEXITCODE = 17
        $ReportPath = 'fixture-report.json'
        $accepted = $true
        try { & $finish } catch { $accepted = $false }
        if ($accepted -ne $AllowUIFailure -or ($AllowUIFailure -and $global:LASTEXITCODE -ne 0)) {
            throw 'UI opt-in must reset native exit status only after mandatory report checks pass.'
        }
    }
} finally {
    if ($null -ne $previousExitCode) { $global:LASTEXITCODE = $previousExitCode.Value }
    else { Remove-Variable -Name LASTEXITCODE -Scope Global -ErrorAction SilentlyContinue }
}
foreach ($mutation in @('MissingReinstall', 'DuplicateStage', 'FalsePass', 'WrongSchema', 'WrongCount', 'SkippedStage')) {
    $report = $good | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    switch ($mutation) {
        MissingReinstall { $report.Checks = @($report.Checks | Where-Object Name -NE 'Reinstall') }
        DuplicateStage { $report.Checks[2].Name = 'Open' }
        FalsePass { $report.Checks[1].Passed = $false }
        WrongSchema { $report.SchemaVersion = 1 }
        WrongCount { $report.Total = 4 }
        SkippedStage { $report.Checks[1].Status = 'Skipped' }
    }
    $accepted = $true
    try { Assert-InstallerReportPassed $report } catch { $accepted = $false }
    if ($accepted) { throw "Malformed installer report accepted: $mutation" }
}

$headless = Resolve-UIOperationEnvironment ([pscustomobject]@{ Windows = $true; UserInteractive = $false })
foreach ($withUI in @($false, $true)) {
    $calls = [Collections.Generic.List[string]]::new()
    $handlers = @{
        Install = { $calls.Add('Install') }; Reinstall = { $calls.Add('Reinstall') }; Uninstall = { $calls.Add('Uninstall') }
        Open = { throw 'Headless Open must not execute.' }; VerifyPreserved = { throw 'Headless profile verification must not execute.' }
        OpenFresh = { throw 'Headless fresh startup must not execute.' }; Environment = $headless
    }
    if ($withUI) { $handlers.UIOperation = { throw 'Headless UI must not execute.' } }
    $report = Invoke-InstallerLifecycle @handlers
    if (($calls -join ',') -cne 'Install,Reinstall,Uninstall' -or (Get-InstallerReportOutcome $report) -cne 'OpenSkipped' -or
        $report.Skipped -ne (3 + [int]$withUI)) { throw 'Headless acceptance must reinstall while explicitly skipping all application/UI stages.' }
    foreach ($mutation in @('MissingEnvironment', 'FalseEnvironment', 'InstallSkipped', 'ReinstallSkipped', 'UninstallSkipped', 'BadCount', 'Failure')) {
        $bad = $report | ConvertTo-Json -Depth 10 | ConvertFrom-Json
        switch ($mutation) {
            MissingEnvironment { $bad.Environment = $null }
            FalseEnvironment { $bad.Environment.Checks.Windows = $false }
            InstallSkipped { $bad.Checks[0].Status = 'Skipped' }
            ReinstallSkipped { $bad.Checks[2].Status = 'Skipped' }
            UninstallSkipped { $bad.Checks[-1].Status = 'Skipped' }
            BadCount { $bad.Skipped = 0 }
            Failure { $bad.Failed = 1 }
        }
        $accepted = $true
        try { Assert-InstallerReportPassed $bad } catch { $accepted = $false }
        if ($accepted) { throw "Invalid headless report accepted: $mutation" }
    }
}
Write-Host '[OK] Install/startup/reinstall/profile/fresh-start/UI/uninstall ordering, failure cleanup, mandatory stages and headless skip contracts.'
