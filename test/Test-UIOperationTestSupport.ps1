$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'UIOperationTestSupport.ps1')

function Assert-Equal($Actual, $Expected, $Message) {
    if ($Actual -cne $Expected) { throw "$Message (expected=$Expected, actual=$Actual)" }
}
function Assert-Rejected($Report, $Message) {
    try { $null = Get-UIOperationReportOutcome $Report }
    catch { return }
    throw $Message
}
function New-DesktopState {
    @{
        Windows = $true; UserInteractive = $true; SessionId = 1; SessionActive = $true
        WindowStationVisible = $true; InputDesktopAccessible = $true
        DesktopName = 'Default'; InputDesktopName = 'Default'
        MonitorCount = 1; DisplayWidth = 1920; DisplayHeight = 1080; DesktopShellAvailable = $true
    }
}
function New-SkipReport {
    $state = New-DesktopState
    $state.InputDesktopAccessible = $false
    $environment = Resolve-UIOperationEnvironment ([pscustomobject]$state)
    New-UIOperationSkipReport $environment
}
function New-PassReport {
    [pscustomobject]@{
        Passed = $true; ExpectedCount = 1
        Results = @([pscustomobject]@{
            Name = 'Application.Example.Run'; Passed = $true
            Evidence = [pscustomobject]@{ CoreProcessId = 10; UiProcessIds = @(11); ProcessesExitedNormally = $true }
        })
    }
}

Assert-Equal (Resolve-UIOperationEnvironment ([pscustomobject](New-DesktopState))).Available $true 'Interactive virtual display should be supported'
foreach ($change in @(
    @('Windows', $false, 'WindowsRequired'), @('UserInteractive', $false, 'NoInteractiveDesktop'),
    @('SessionId', 0, 'NoInteractiveDesktop'), @('SessionActive', $false, 'NoInteractiveDesktop'),
    @('WindowStationVisible', $false, 'NoInteractiveDesktop'), @('InputDesktopAccessible', $false, 'NoInteractiveDesktop'),
    @('DesktopName', 'Other', 'NoInteractiveDesktop'), @('InputDesktopName', 'Winlogon', 'NoInteractiveDesktop'),
    @('MonitorCount', 0, 'NoDisplay'), @('DisplayWidth', 0, 'NoDisplay'), @('DisplayHeight', 0, 'NoDisplay'),
    @('DesktopShellAvailable', $false, 'NoDesktopShell')
)) {
    $state = New-DesktopState
    $state[$change[0]] = $change[1]
    $environment = Resolve-UIOperationEnvironment ([pscustomobject]$state)
    Assert-Equal $environment.Available $false "Unavailable desktop: $($change[0])"
    Assert-Equal $environment.ReasonCode $change[2] "Skip reason: $($change[0])"
}
Assert-Equal (Get-UIOperationReportOutcome (New-SkipReport)) 'Skipped' 'Explicit unavailable-environment report'
Assert-Equal (Get-UIOperationReportOutcome (New-PassReport)) 'Passed' 'Existing host report remains supported'
foreach ($mutation in @('Passed','Count','Results','Available','Reason','Code','MissingFlag')) {
    $report = New-SkipReport
    switch ($mutation) {
        'Passed' { $report.Passed = $true }
        'Count' { $report.ExpectedCount = 1 }
        'Results' { $report.Results = @([pscustomobject]@{ Name = 'Application.Failed.Run'; Passed = $false }) }
        'Available' { $report.Environment.Available = $true }
        'Reason' { $report.Environment.Reason = '' }
        'Code' { $report.SkipReason = 'Timeout' }
        'MissingFlag' { $report.PSObject.Properties.Remove('Skipped') }
    }
    Assert-Rejected $report "Malformed skip accepted: $mutation"
}
$report = New-PassReport
$report.Results[0].Passed = $false
Assert-Rejected $report 'UI failure must remain a failure'
$report = New-PassReport
$report.ExpectedCount = 2
Assert-Rejected $report 'Incomplete results must fail'
$report = New-PassReport
$report.ExpectedCount = 2; $report.Results += $report.Results[0]
Assert-Rejected $report 'Duplicate cases must fail'
$report = New-PassReport
$report.Results[0].Evidence.ProcessesExitedNormally = $false
Assert-Rejected $report 'Abnormal process exit must fail'
Assert-Rejected $null 'Missing report must fail'
$extendedModules = @('GeneralSettings','WallpaperLibrary','StaticEditor','WebEditor','ModelInference','ScreenSaverLifecycle','Rendering')
$hardwareCases = @('StartupPolicyAndOptions','SoftwareDrawingSaveReopenAndExport','SoftwareWebEditSaveReopenAndExport','DeviceChoiceAndEnhancement')
$extendedCases = @($extendedModules | ForEach-Object { "Application.$_.Fixture" }) +
    @($hardwareCases | ForEach-Object { "Application.HardwareAcceleration.$_" })
$extended = New-PassReport
$extended.Results = @($extendedCases | ForEach-Object {
    [pscustomobject]@{ Name = $_; Passed = $true
        Evidence = [pscustomobject]@{ CoreProcessId = 10; UiProcessIds = @(11); ProcessesExitedNormally = $true } }
})
$extended.ExpectedCount = $extended.Results.Count
Assert-ExtendedUIModules $extended
$completeResults = $extended.Results
$extended.Results = @($extended.Results | Where-Object Name -NotLike 'Application.HardwareAcceleration.*')
$extended.ExpectedCount = $extended.Results.Count
$rejected = $false
try { Assert-ExtendedUIModules $extended } catch { $rejected = $true }
Assert-Equal $rejected $true 'Installed extended acceptance must reject a missing hardware-acceleration module'
foreach ($case in $hardwareCases) {
    $extended.Results = @($completeResults | Where-Object Name -CNE "Application.HardwareAcceleration.$case")
    $extended.ExpectedCount = $extended.Results.Count
    Assert-Equal (Get-UIOperationReportOutcome $extended) 'Passed' 'Reduced report still has consistent counts and outcomes'
    $rejected = $false
    try { Assert-ExtendedUIModules $extended } catch { $rejected = $true }
    Assert-Equal $rejected $true "Installed extended acceptance must reject missing hardware case: $case"
}
Write-Host '[OK] UI desktop eligibility and report validation contracts passed (no UI, lock or registry operations).'
