$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'UIOperationTestSupport.ps1')
. (Join-Path $PSScriptRoot 'CIUiDisplay.ps1')

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
$originalActions = $env:GITHUB_ACTIONS; $originalRunner = $env:RUNNER_ENVIRONMENT
try {
    $smallState = New-DesktopState
    $smallState.DisplayWidth = 1024; $smallState.DisplayHeight = 768
    $small = Resolve-UIOperationEnvironment ([pscustomobject]$smallState)
    $large = Resolve-UIOperationEnvironment ([pscustomobject](New-DesktopState))
    $unexpected = { throw 'This environment must not modify or reprobe the desktop.' }
    foreach ($runner in @(@('', ''), @('true', 'self-hosted'), @('false', 'github-hosted'))) {
        $env:GITHUB_ACTIONS = $runner[0]; $env:RUNNER_ENVIRONMENT = $runner[1]
        Assert-Equal (Initialize-CIUIOperationDisplay $small $unexpected $unexpected) $small 'Local/self-hosted display stays unchanged'
    }
    $env:GITHUB_ACTIONS = 'true'; $env:RUNNER_ENVIRONMENT = 'github-hosted'
    Assert-Equal (Initialize-CIUIOperationDisplay $large $unexpected $unexpected) $large 'A sufficiently large CI display stays unchanged'
    $unavailable = (New-SkipReport).Environment
    Assert-Equal (Initialize-CIUIOperationDisplay $unavailable $unexpected $unexpected) $unavailable 'Unavailable input desktop is never changed'
    $calls = [Collections.Generic.List[string]]::new()
    $updated = Initialize-CIUIOperationDisplay $small { param($width, $height) $calls.Add("${width}x${height}") } { $large }
    Assert-Equal ($calls -join ',') '1920x1080' 'CI configures its disposable display exactly once'
    Assert-Equal $updated.Checks.DisplayWidth 1920 'The prepared resolution comes from a fresh probe'
    Assert-Equal $updated.OriginalDisplay.DisplayWidth 1024 'Retain the original display in diagnostic evidence'
    $rejected = $false
    try { $null = Initialize-CIUIOperationDisplay $small { throw 'Unsupported display mode' } $unexpected } catch { $rejected = $true }
    Assert-Equal $rejected $true 'Unsupported display preparation fails instead of passing UI'
    $rejected = $false
    try { $null = Initialize-CIUIOperationDisplay $small {} { $small } } catch { $rejected = $true }
    Assert-Equal $rejected $true 'A no-op display change cannot pass preparation'
}
finally { $env:GITHUB_ACTIONS = $originalActions; $env:RUNNER_ENVIRONMENT = $originalRunner }
# Compile the actual interop declaration and check its ABI without calling user32.
$displayAst = (Get-Command Set-CIUIOperationResolution).ScriptBlock.Ast
$displaySource = $displayAst.Find({ param($node)
    $node -is [System.Management.Automation.Language.StringConstantExpressionAst] -and $node.Value.StartsWith('using System;')
}, $true).Value
if (-not ('VirtualPaper.CIUiDisplay' -as [type])) { Add-Type -TypeDefinition $displaySource }
$modeType = [VirtualPaper.CIUiDisplay].GetNestedType('DisplayMode', [Reflection.BindingFlags]::NonPublic)
Assert-Equal ([Runtime.InteropServices.Marshal]::SizeOf([type]$modeType)) 220 'DEVMODEW public structure size'
Assert-Equal ([Runtime.InteropServices.Marshal]::OffsetOf($modeType, 'Width').ToInt32()) 172 'DEVMODEW width offset'
Assert-Equal ([Runtime.InteropServices.Marshal]::OffsetOf($modeType, 'Height').ToInt32()) 176 'DEVMODEW height offset'
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
$component = [pscustomobject]@{ Passed = $true; ExpectedCount = 1
    Results = @([pscustomobject]@{ Name = 'Localization.LongText.Layout'; Passed = $true; Evidence = @{} }) }
Assert-Rejected $component 'Full acceptance must not accept component-only results'
Assert-Equal (Get-UIOperationReportOutcome $component -FocusedModule Localization) 'Passed' 'Explicit component group'
Assert-Equal (Get-UIOperationReportOutcome $component -FocusedModule Localization.LongText.Layout) 'Passed' 'Exact component case'
Assert-Equal (Get-UIOperationReportOutcome $component -FocusedModule GeneralEffect -AllowFailure) 'Failed' 'Wrong focused group'
$component.Results[0].Passed = $false
Assert-Equal (Get-UIOperationReportOutcome $component -FocusedModule Localization -AllowFailure) 'Failed' 'Focused failures remain failures'
$component.Results[0].Passed = $true; $component.ExpectedCount = 2
Assert-Equal (Get-UIOperationReportOutcome $component -FocusedModule Localization -AllowFailure) 'Failed' 'Focused reports must be complete'
$application = New-PassReport
$application.Results[0].Evidence.ProcessesExitedNormally = $false
Assert-Equal (Get-UIOperationReportOutcome $application -FocusedModule Example -AllowFailure) 'Failed' 'Focused application exit must remain normal'
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
# Exercise the runner's actual error handler without launching any UI/process.
$runnerAst = [System.Management.Automation.Language.Parser]::ParseFile("$PSScriptRoot/Run-UIOperationTests.ps1", [ref]$null, [ref]$null)
$handler = $runnerAst.Find({ param($node)
    $node -is [System.Management.Automation.Language.CatchClauseAst] -and
        $node.Body.Extent.Text.Contains('New-UIOperationFailureReport') -and
        $node.Body.Extent.Text.Contains('$process.HasExited')
}, $true).Body.Extent.Text
$failureHandler = [scriptblock]::Create($handler.Substring(1, $handler.Length - 2))
$reportFullPath = Join-Path ([IO.Path]::GetTempPath()) "VirtualPaper.UIHostFailure-$([guid]::NewGuid().ToString('N')).json"
$environment = Resolve-UIOperationEnvironment ([pscustomobject](New-DesktopState))
try {
    foreach ($process in @($null, [pscustomobject]@{ HasExited = $false }, [pscustomobject]@{ HasExited = $true; ExitCode = -1073741189 })) {
        $rejected = $false
        try { try { throw 'Fixture host failure or timeout' } catch { & $failureHandler } } catch { $rejected = $true }
        Assert-Equal $rejected $true 'The runner must still fail after writing the diagnostic report'
        $failure = Get-Content -LiteralPath $reportFullPath -Raw | ConvertFrom-Json
        Assert-Equal $failure.Status 'Failed' 'Host errors must not be reported as passed or skipped'
        Assert-Equal $failure.Error 'Fixture host failure or timeout' 'Preserve the actual host failure'
        Assert-Equal $failure.HostExitCode $(if ($process.HasExited) { $process.ExitCode } else { $null }) 'Capture only an exited host code'
        Assert-Equal @($failure.Results).Count 0 'Do not invent completed case results'
        Assert-Equal (Get-UIOperationReportOutcome $failure -AllowFailure) 'Failed' 'CI may record a host failure'
        Assert-Rejected $failure 'Host failures must still block strict local acceptance'
        Remove-Item -LiteralPath $reportFullPath
    }
    $setupHandler = $runnerAst.Find({ param($node)
        $node -is [System.Management.Automation.Language.CatchClauseAst] -and
            $node.Body.Extent.Text.Contains('New-UIOperationFailureReport') -and
            -not $node.Body.Extent.Text.Contains('$process.HasExited')
    }, $true).Body.Extent.Text
    $setupFailureHandler = [scriptblock]::Create($setupHandler.Substring(1, $setupHandler.Length - 2))
    $rejected = $false
    try { try { throw 'Fixture display preparation failure' } catch { & $setupFailureHandler } } catch { $rejected = $true }
    Assert-Equal $rejected $true 'Display preparation failure must still throw after writing its report'
    $failure = Get-Content -LiteralPath $reportFullPath -Raw | ConvertFrom-Json
    Assert-Equal $failure.Error 'Fixture display preparation failure' 'Preserve the display preparation error'
    Assert-Equal $failure.HostExitCode $null 'Do not invent an exit code for an unstarted host'
    Assert-Equal (Get-UIOperationReportOutcome $failure -AllowFailure) 'Failed' 'Display preparation cannot become a passing/skipped UI result'
    Assert-Rejected $failure 'Display preparation failure blocks strict local acceptance'
    Remove-Item -LiteralPath $reportFullPath
    foreach ($original in @((New-PassReport | ConvertTo-Json -Depth 8), '{invalid report')) {
        [IO.File]::WriteAllText($reportFullPath, $original)
        try { try { throw 'Failure after an existing report' } catch { & $failureHandler } } catch { }
        Assert-Equal ([IO.File]::ReadAllText($reportFullPath)) $original 'Never replace an existing complete, partial or malformed host report'
    }
} finally {
    if (Test-Path -LiteralPath $reportFullPath) { Remove-Item -LiteralPath $reportFullPath }
}
# Read the registered cases independently of the report-gate policy.
$source = Get-Content -LiteralPath "$PSScriptRoot/VirtualPaper.UIOperationTest/App.xaml.cs" -Raw
$extendedCases = @([regex]::Matches($source, 'new UiTestCase\("([^"]+)"') | ForEach-Object { $_.Groups[1].Value } |
    Where-Object { $_ -notlike 'Application.MsixProfile.*' -and $_ -cne 'Application.ScreenSaverIdle.RealIdleInputWakeAndRepeat' })
Assert-Equal ($extendedCases.Count -gt 0) $true 'Extended registration inventory must not be empty'
$requiredCases = @(Get-RequiredExtendedUICases)
Assert-Equal (@($requiredCases | Sort-Object -Unique).Count) $requiredCases.Count 'Required cases must be unique'
Assert-Equal (@(Compare-Object $extendedCases $requiredCases -CaseSensitive).Count) 0 'Required policy must match the selected extended registration inventory'
$extended = New-PassReport
$extended.Results = @($extendedCases | ForEach-Object {
    [pscustomobject]@{ Name = $_; Passed = $true
        Evidence = [pscustomobject]@{ CoreProcessId = 10; UiProcessIds = @(11); ProcessesExitedNormally = $true } }
})
$extended.ExpectedCount = $extended.Results.Count
Assert-ExtendedUIModules $extended
$completeResults = $extended.Results
foreach ($case in $extendedCases) {
    $extended.Results = @($completeResults | Where-Object Name -CNE $case)
    $extended.ExpectedCount = $extended.Results.Count
    Assert-Equal (Get-UIOperationReportOutcome $extended) 'Passed' 'A reduced report remains internally consistent'
    $rejected = $false
    try { Assert-ExtendedUIModules $extended } catch { $rejected = $true }
    Assert-Equal $rejected $true "Installed extended acceptance must reject missing case: $case"
    $extended.Results = @($completeResults + ($completeResults | Where-Object Name -CEQ $case))
    $extended.ExpectedCount = $extended.Results.Count
    $rejected = $false
    try { Assert-ExtendedUIModules $extended } catch { $rejected = $true }
    Assert-Equal $rejected $true "Installed extended acceptance must reject duplicate case: $case"
}
Write-Host '[OK] UI desktop eligibility and report validation contracts passed (no UI, lock or registry operations).'
