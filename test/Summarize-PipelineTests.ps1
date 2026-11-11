#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Root,
    [Parameter(Mandatory)][string] $ArtifactsRoot,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string] $SourceSha,
    [Parameter(Mandatory)][string] $JobResultsJson,
    [bool] $MsixEnabled = $true
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
        & "$PSScriptRoot/Summarize-TestResults.ps1" -Root $Root -RequireFullAcceptance
        $projects = @(Get-ProjectTestReports -Root $Root -RequireCompleteExecution)
        $details.ProjectTestsPassed = ($projects | Measure-Object Passed -Sum).Sum
        $details.Coverage = @(Get-TestCoverageRows -Root $Root)
    } finally { $env:GITHUB_OUTPUT = $previousOutput }
}
Check 'EXE artifact identity' {
    $exe = Get-PackageArtifact "$ArtifactsRoot/exe/package.json" EXE $SourceSha
    foreach ($path in @('test-results-installer/installer-results.json', 'test-results-ui-operation/installed-acceptance.json')) {
        $report = Get-Content -LiteralPath (Join-Path $Root $path) -Raw | ConvertFrom-Json
        Assert-InstallerReportPassed $report
        if ($report.InstallerSha256 -ine $exe.PackageSha256 -or [version]$report.PackageVersion -ne [version]$exe.PackageVersion) {
            throw 'EXE installed acceptance tested a different installer.'
        }
    }
    $ui = Get-Content -LiteralPath "$Root/test-results-ui-operation/ui-operation-results.json" -Raw | ConvertFrom-Json
    Assert-ExtendedUIModules $ui
    $details.EXE = @{ ProductVersion = $exe.PackageVersion; PackageVersion = $exe.PackageVersion
        PackageSha256 = $exe.PackageSha256; UICases = @($ui.Results).Count }
}
if ($MsixEnabled) {
    Check 'MSIX smoke / installed UI / artifact identity' {
        $artifact = Get-PackageArtifact "$ArtifactsRoot/msix/package.json" MSIX $SourceSha
        if (-not $details.EXE -or [version]$artifact.ProductVersion -ne [version]$details.EXE.ProductVersion) {
            throw 'EXE and MSIX product versions differ.'
        }
        $packageNames = @()
        $caseCount = 0
        foreach ($mode in @('Smoke', 'UI')) {
            $directory = Join-Path $Root "test-results-msix-$($mode.ToLowerInvariant())"
            $report = Get-Content -LiteralPath (Join-Path $directory 'msix-acceptance.json') -Raw | ConvertFrom-Json
            Assert-MsixAcceptanceReport $report $mode
            if ($report.PackageSha256 -ine $artifact.PackageSha256 -or $report.SourceSha -cne $SourceSha) {
                throw 'MSIX installed acceptance tested a different package or snapshot.'
            }
            $packageNames += $report.PackageFullName
            if ($mode -eq 'UI') {
                $ui = Assert-MsixUIReport "$directory/extended-ui/ui-operation-results.json" $report.PackageFullName -Extended
                $caseCount += @($ui.Results).Count
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
            PackageSha256 = $artifact.PackageSha256; UploadSha256 = $(if ($artifact.Store) { $artifact.UploadSha256 } else { $null }); UICases = $caseCount }
    }
} else { $rows.Add(@{ Name = 'MSIX'; Status = 'NotSelected'; Error = $null }) }
$execution = if ($env:GITHUB_ACTIONS -eq 'true') { 'GitHubActions' } else { 'Local' }
$report = @{ Passed = $errors.Count -eq 0; SourceSha = $SourceSha; Execution = $execution; MsixEnabled = $MsixEnabled; Checks = $rows.ToArray(); Details = $details }
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
if ($env:GITHUB_OUTPUT) { 'ui_outcome=Passed' >> $env:GITHUB_OUTPUT }
