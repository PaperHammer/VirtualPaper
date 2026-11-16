#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Root,
    [Parameter(Mandatory)][string] $ArtifactsRoot,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string] $SourceSha,
    [Parameter(Mandatory)][string] $JobResultsJson,
    [bool] $MsixEnabled = $true,
    [switch] $AllowUIFailure
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/PackageArtifactSupport.ps1"
. "$PSScriptRoot/MsixTestSupport.ps1"
. "$PSScriptRoot/UIOperationTestSupport.ps1"
. "$PSScriptRoot/InstallerTestSupport.ps1"
. "$PSScriptRoot/TestReportSupport.ps1"
[void](New-Item -ItemType Directory -Path $Root -Force)
$errors = [Collections.Generic.List[string]]::new()
$rows = [Collections.Generic.List[object]]::new()
$details = [ordered]@{ ProjectTestsPassed = 0; Coverage = @(); EXE = $null; MSIX = $null }
function Check([string] $Name, [scriptblock] $Action) {
    try { & $Action | Out-Host; $rows.Add(@{ Name = $Name; Status = 'Passed'; Error = $null }) }
    catch { $message = $_.Exception.Message; $errors.Add("${Name}: $message"); $rows.Add(@{ Name = $Name; Status = 'Failed'; Error = $message }) }
}
function Read-InstalledUIReport([string] $Path, [string] $JobId, [bool] $LifecyclePassed) {
    # A tolerated run failure still requires an uploaded, readable result report.
    $ui = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($null -eq $ui -or $ui.Passed -isnot [bool] -or $ui.Results -isnot [array] -or
        $null -eq $ui.ExpectedCount -or $ui.ExpectedCount -notmatch '^\d+$') {
        throw "Missing or malformed installed UI report: $Path"
    }
    $outcome = Get-UIOperationReportOutcome $ui -AllowFailure:($AllowUIFailure -and -not $ui.Passed)
    if (($ui.Status -eq 'Skipped' -or $ui.Skipped -eq $true) -and $outcome -ne 'Skipped') {
        throw "Invalid installed UI skip report: $Path"
    }
    if ($outcome -eq 'Passed') { Assert-ExtendedUIModules $ui }
    elseif (-not $AllowUIFailure) { throw 'Installed UI acceptance did not pass.' }
    $runResult = $jobs[$JobId].outputs.ui_run_result
    if ($AllowUIFailure -and $runResult -cnotin @('success', 'failure', 'skipped')) {
        throw "Missing installed UI step outcome: $JobId"
    }
    if (-not $AllowUIFailure -and $runResult -and $runResult -cne 'success') { throw "Installed UI step did not pass: $JobId" }
    if ($outcome -eq 'Passed' -and (-not $LifecyclePassed -or ($runResult -and $runResult -cne 'success'))) { $outcome = 'Failed' }
    return @{ Report = $ui; Outcome = $outcome; RunResult = $runResult }
}
$jobs = $JobResultsJson | ConvertFrom-Json -AsHashtable
Check 'Job results' {
    $required = @('packages-ready', 'package-exe', 'test-exe-acceptance') + @(Get-ProjectTestSpecs | ForEach-Object Id)
    if ($MsixEnabled) { $required += 'package-msix', 'test-msix-acceptance' }
    foreach ($name in $required) {
        if (-not $jobs.ContainsKey($name) -or $jobs[$name].result -cne 'success') { throw "Required job $name did not pass." }
    }
    if (-not $MsixEnabled) {
        foreach ($name in @('package-msix', 'test-msix-acceptance')) {
            if (-not $jobs.ContainsKey($name) -or $jobs[$name].result -cne 'skipped') { throw "Disabled MSIX job $name must be skipped." }
        }
    }
    foreach ($entry in $jobs.GetEnumerator()) {
        if (-not $MsixEnabled -and $entry.Key -in @('package-msix','test-msix-acceptance')) { continue }
        if ($entry.Value.result -cne 'success') { throw "Required prerequisite $($entry.Key) did not pass." }
    }
}
Check 'Shared project tests / EXE smoke / installed UI' {
    $previousOutput = $env:GITHUB_OUTPUT
    try {
        $env:GITHUB_OUTPUT = ''
        & "$PSScriptRoot/Summarize-TestResults.ps1" -Root $Root -RequireFullAcceptance -AllowUIFailure:$AllowUIFailure
        $projects = @(Get-ProjectTestReports -Root $Root -RequireCompleteExecution)
        $details.ProjectTestsPassed = ($projects | Measure-Object Passed -Sum).Sum
        $details.Coverage = @(Get-TestCoverageRows -Root $Root)
    } finally { $env:GITHUB_OUTPUT = $previousOutput }
}
Check 'EXE artifact identity' {
    $exe = Get-PackageArtifact "$ArtifactsRoot/exe/package.json" EXE $SourceSha
    $details.EXE = @{ ProductVersion = $exe.PackageVersion; PackageVersion = $exe.PackageVersion
        PackageSha256 = $exe.PackageSha256; UICases = 0; UIOutcome = $null; UIRunResult = $null }
    $lifecyclePassed = $true
    foreach ($path in @('test-results-installer/installer-results.json', 'test-results-ui-operation/installed-acceptance.json')) {
        $report = Get-Content -LiteralPath (Join-Path $Root $path) -Raw | ConvertFrom-Json
        if ($path.StartsWith('test-results-installer/') -or -not $AllowUIFailure) { Assert-InstallerReportPassed $report }
        else {
            if ($report.SchemaVersion -ne 3 -or $report.Suite -cne 'InstallerSmoke' -or -not @($report.Checks).Count) {
                throw 'Installed EXE UI lifecycle report is malformed.'
            }
            try { Assert-InstallerReportPassed $report } catch { $lifecyclePassed = $false }
        }
        if ($report.InstallerSha256 -ine $exe.PackageSha256 -or [version]$report.PackageVersion -ne [version]$exe.PackageVersion) {
            throw 'EXE installed acceptance tested a different installer.'
        }
    }
    $ui = Read-InstalledUIReport "$Root/test-results-ui-operation/ui-operation-results.json" 'test-exe-acceptance' $lifecyclePassed
    $details.EXE.UICases = @($ui.Report.Results).Count
    $details.EXE.UIOutcome = $ui.Outcome
    $details.EXE.UIRunResult = $ui.RunResult
}
if ($MsixEnabled) {
    Check 'MSIX smoke / installed UI / artifact identity' {
        $artifact = Get-PackageArtifact "$ArtifactsRoot/msix/package.json" MSIX $SourceSha
        if ($details.EXE -and [version]$artifact.ProductVersion -ne [version]$details.EXE.ProductVersion) {
            throw 'EXE and MSIX product versions differ.'
        }
        $packageNames = @()
        $caseCount = 0
        $uiOutcome = $null; $uiRunResult = $null
        foreach ($mode in @('Smoke', 'UI')) {
            $directory = Join-Path $Root "test-results-msix-$($mode.ToLowerInvariant())"
            $report = Get-Content -LiteralPath (Join-Path $directory 'msix-acceptance.json') -Raw | ConvertFrom-Json
            if ($mode -eq 'Smoke' -or -not $AllowUIFailure) { Assert-MsixAcceptanceReport $report $mode }
            elseif ($report.Mode -cne 'UI' -or -not $report.PackageFullName -or -not @($report.Stages).Count) {
                throw 'Installed MSIX UI lifecycle report is malformed.'
            }
            if ($report.PackageSha256 -ine $artifact.PackageSha256 -or $report.SourceSha -cne $SourceSha) {
                throw 'MSIX installed acceptance tested a different package or snapshot.'
            }
            $packageNames += $report.PackageFullName
            if ($mode -eq 'UI') {
                $path = "$directory/extended-ui/ui-operation-results.json"
                $lifecyclePassed = $true
                try { Assert-MsixAcceptanceReport $report UI } catch { $lifecyclePassed = $false }
                $ui = Read-InstalledUIReport $path 'test-msix-acceptance' $lifecyclePassed
                $null = Assert-MsixUIReport $path $report.PackageFullName -Extended -AllowFailure:$AllowUIFailure
                $caseCount += @($ui.Report.Results).Count
                $uiOutcome = $ui.Outcome; $uiRunResult = $ui.RunResult
            } else {
                foreach ($stage in @('first-run', 'retained-profile', 'fresh-profile')) {
                    $ui = Assert-MsixUIReport "$directory/$stage/ui-operation-results.json" $report.PackageFullName
                    $caseCount += @($ui.Results).Count
                }
                foreach ($property in @('InitialDeployment', 'MissingFolderRestored', 'ExistingDataPreserved', 'SavedDirectoryHonored', 'UninstallDataPreserved')) {
                    if ($report.BuiltInWallpapers.$property -ne $true) { throw "Missing MSIX wallpaper evidence: $property" }
                }
            }
        }
        if (@($packageNames | Select-Object -Unique).Count -ne 1) { throw 'MSIX smoke and UI installed different package identities.' }
        $details.MSIX = @{ ProductVersion = $artifact.ProductVersion; PackageVersion = $artifact.PackageVersion
            PackageSha256 = $artifact.PackageSha256; UploadSha256 = $(if ($artifact.Store) { $artifact.UploadSha256 } else { $null })
            UICases = $caseCount; UIOutcome = $uiOutcome; UIRunResult = $uiRunResult }
    }
} else { $rows.Add(@{ Name = 'MSIX'; Status = 'NotSelected'; Error = $null }) }
$execution = if ($env:GITHUB_ACTIONS -eq 'true') { 'GitHubActions' } else { 'Local' }
$uiOutcome = 'Passed'
foreach ($channel in @('EXE', 'MSIX')) {
    $data = $details[$channel]
    if (-not $data -or -not $data.UIOutcome) { continue }
    if ($data.UIOutcome -eq 'Failed' -or ($data.UIOutcome -eq 'Skipped' -and $uiOutcome -eq 'Passed')) { $uiOutcome = $data.UIOutcome }
    $name = "$channel extended UI" + $(if ($AllowUIFailure) { ' (non-blocking)' } else { '' })
    $rows.Add(@{ Name = $name; Status = $data.UIOutcome; Error = $null })
}
$report = @{ Passed = $errors.Count -eq 0; SourceSha = $SourceSha; Execution = $execution; MsixEnabled = $MsixEnabled
    UIOutcome = $uiOutcome; AllowUIFailure = $AllowUIFailure.IsPresent; Checks = $rows.ToArray(); Details = $details }
$report | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $Root 'pipeline-summary.json') -Encoding utf8NoBOM
$lines = @('# Package acceptance', '', "Execution: $execution; source baseline: $SourceSha", '', '| Check | Result |', '| --- | --- |')
$lines += @($rows | ForEach-Object { "| $($_.Name) | $($_.Status) |" })
$lines += @('', "Shared project tests passed: $($details.ProjectTestsPassed)", '', '| Channel | Product / package version | UI cases | Package SHA256 |', '| --- | --- | ---: | --- |')
foreach ($channel in @('EXE', 'MSIX')) {
    $data = $details[$channel]
    if ($data) { $lines += "| $channel | $($data.ProductVersion) / $($data.PackageVersion) | $($data.UICases) | $($data.PackageSha256) |" }
}
if ($details.MSIX -and $details.MSIX.UploadSha256) { $lines += @('', "Store upload SHA256: $($details.MSIX.UploadSha256)") }
$lines += @('', '| Project | Covered lines | Valid lines |', '| --- | ---: | ---: |')
$lines += @($details.Coverage | ForEach-Object { "| $($_.Project) | $($_.Covered) | $($_.Valid) |" })
$lines += @('', $errors.ToArray())
$lines | Set-Content (Join-Path $Root 'pipeline-summary.md') -Encoding utf8NoBOM
if ($env:GITHUB_STEP_SUMMARY) { $lines -join "`n" >> $env:GITHUB_STEP_SUMMARY }
if ($errors.Count) { throw ($errors -join "`n") }
if ($env:GITHUB_OUTPUT) { "ui_outcome=$uiOutcome" >> $env:GITHUB_OUTPUT }
