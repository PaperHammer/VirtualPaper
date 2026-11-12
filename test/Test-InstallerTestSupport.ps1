$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/InstallerTestSupport.ps1"
foreach ($language in @('english', 'en-US', 'en')) {
    if ((Get-InstallerApplicationLanguage $language) -cne 'en-US') { throw 'English installer marker compatibility failed.' }
}
foreach ($language in @('chinese', 'zh-CN', 'zh-Hans')) {
    if ((Get-InstallerApplicationLanguage $language) -cne 'zh-CN') { throw 'Chinese installer marker compatibility failed.' }
}
foreach ($entry in (Get-LanguageCatalog).Languages) {
    foreach ($marker in @($entry.Code, $entry.InstallerName) + @($entry.Aliases)) {
        if ((Get-InstallerApplicationLanguage $marker) -cne $entry.Code) { throw "Installer language does not resolve to its catalog locale: $marker" }
    }
}
$rejected = $false
try { $null = Get-InstallerApplicationLanguage 'unsupported' } catch { $rejected = $true }
if (-not $rejected) { throw 'An unknown installer marker must fail acceptance.' }

# Callbacks only: no installer, GUI, registry or system configuration is touched.
$stages = @('Install', 'Open', 'Reinstall', 'VerifyPreserved', 'OpenFresh', 'Uninstall')
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
        throw "Incorrect failure/cleanup order: $failure"
    }
    $accepted = $true
    try { Assert-InstallerReportPassed $report } catch { $accepted = $false }
    if ($accepted -ne ($failure -eq '')) { throw "Every installer smoke stage must pass: $failure" }
}

$good = Invoke-InstallerLifecycle -Install {} -Open {} -Reinstall {} -VerifyPreserved {} -OpenFresh {} -Uninstall {}
foreach ($mutation in @('MissingReinstall', 'DuplicateStage', 'FalsePass', 'WrongSchema', 'WrongSuite', 'WrongCount', 'SkippedStage')) {
    $report = $good | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    switch ($mutation) {
        MissingReinstall { $report.Checks = @($report.Checks | Where-Object Name -NE 'Reinstall') }
        DuplicateStage { $report.Checks[2].Name = 'Open' }
        FalsePass { $report.Checks[1].Passed = $false }
        WrongSchema { $report.SchemaVersion = 2 }
        WrongSuite { $report.Suite = 'InstallerLifecycle' }
        WrongCount { $report.Total = 4 }
        SkippedStage { $report.Checks[1].Status = 'Skipped' }
    }
    $accepted = $true
    try { Assert-InstallerReportPassed $report } catch { $accepted = $false }
    if ($accepted) { throw "Malformed installer smoke report accepted: $mutation" }
}

$headless = Resolve-UIOperationEnvironment ([pscustomobject]@{ Windows = $true; UserInteractive = $false })
$calls = [Collections.Generic.List[string]]::new()
$report = Invoke-InstallerLifecycle -Install { $calls.Add('Install') } -Reinstall { $calls.Add('Reinstall') } `
    -Uninstall { $calls.Add('Uninstall') } -Open { throw 'Headless Open must not execute.' } `
    -VerifyPreserved { throw 'Headless profile verification must not execute.' } `
    -OpenFresh { throw 'Headless fresh startup must not execute.' } -Environment $headless
if (($calls -join ',') -cne 'Install,Reinstall,Uninstall' -or $report.Skipped -ne 3) {
    throw 'Headless diagnostics must retain skipped startup/profile evidence and still uninstall.'
}
$accepted = $true
try { Assert-InstallerReportPassed $report } catch { $accepted = $false }
if ($accepted) { throw 'Skipped startup is never a passing installer smoke result.' }

# Exercise the production runner's final report/exit-code gate without installing.
$runnerText = Get-Content -LiteralPath "$PSScriptRoot/Run-InstallerTests.ps1" -Raw
$finishAt = $runnerText.IndexOf('Assert-InstallerReportPassed $report')
if ($finishAt -lt 0) { throw 'Installer smoke result gate missing.' }
$finish = [scriptblock]::Create($runnerText.Substring($finishAt))
$previousExitCode = Get-Variable -Name LASTEXITCODE -Scope Global -ErrorAction SilentlyContinue
try {
    foreach ($passed in @($false, $true)) {
        $report = if ($passed) { $good } else { $report }
        $global:LASTEXITCODE = 17
        $ReportPath = 'fixture-report.json'
        $accepted = $true
        try { & $finish } catch { $accepted = $false }
        if ($accepted -ne $passed -or ($passed -and $global:LASTEXITCODE -ne 0)) {
            throw 'Installer smoke may reset native exit status only after every stage passed.'
        }
    }
} finally {
    if ($null -ne $previousExitCode) { $global:LASTEXITCODE = $previousExitCode.Value }
    else { Remove-Variable -Name LASTEXITCODE -Scope Global -ErrorAction SilentlyContinue }
}
Write-Host '[OK] Independent installer smoke ordering, mandatory stages, failure cleanup and headless rejection.'
