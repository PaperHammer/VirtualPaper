$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TestReportSupport.ps1')
. (Join-Path $PSScriptRoot 'InstallerTestSupport.ps1')

function Assert-ReviewCondition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Assert-ReportThrows([scriptblock]$Action, [string]$Message) {
    $threw = $false
    try { & $Action | Out-Null } catch { $threw = $true }
    Assert-ReviewCondition $threw "Expected rejection: $Message"
}
function Write-Trx([string]$Path, [string]$Counters = 'total="3" executed="2" passed="2" failed="0" aborted="0"', [string]$Outcome = 'Completed', [string]$Output = '') {
    New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force | Out-Null
    [IO.File]::WriteAllText($Path, "<TestRun xmlns=`"http://microsoft.com/schemas/VisualStudio/TeamTest/2010`"><ResultSummary outcome=`"$Outcome`"><Counters $Counters/><Output><StdOut>$Output</StdOut></Output></ResultSummary></TestRun>")
}

$resultsRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../TestResults'))
$fixture = Join-Path $resultsRoot "report-support-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$oldOutput = $env:GITHUB_OUTPUT; $oldSummary = $env:GITHUB_STEP_SUMMARY
try {
    $path = Join-Path $fixture 'sample.trx'
    Write-Trx $path
    $report = Assert-TrxReportPassed $path
    Assert-ReviewCondition ($report.Total -eq 3 -and $report.Executed -eq 2) 'Passing TRX with skipped cases must be accepted.'
    foreach ($case in @(
        @{ Counters = 'total="0" executed="0" passed="0" failed="0" aborted="0"' },
        @{ Counters = 'total="1" executed="0" passed="0" failed="0" aborted="0"' },
        @{ Counters = 'total="2" executed="2" passed="1" failed="1" aborted="0"' },
        @{ Counters = 'total="2" executed="2" passed="1" failed="0" aborted="1"' },
        @{ Counters = 'total="2" executed="2" passed="1" failed="0" aborted="0" warning="1"' },
        @{ Counters = 'total="2" executed="2" passed="1" failed="0" aborted="0"' },
        @{ Counters = 'total="2" executed="2" passed="1" failed="0" aborted="0" timeout="1"' },
        @{ Counters = 'total="invalid" executed="2" passed="2" failed="0" aborted="0"' },
        @{ Counters = 'total="3" executed="2" passed="2" failed="0"' },
        @{ Outcome = 'Failed' },
        @{ Output = 'Unhandled exception. test host crashed' }
    )) {
        Write-Trx -Path $path @case
        Assert-ReportThrows { Assert-TrxReportPassed $path } 'Invalid, empty, aborted or failed report'
    }
    [IO.File]::WriteAllText($path, '<Other/>')
    Assert-ReportThrows { Assert-TrxReportPassed $path } 'Missing TRX summary'
    Remove-Item -LiteralPath $path
    Assert-ReportThrows { Assert-TrxReportPassed $path } 'Missing report file'

    $rawRoot = Join-Path $fixture 'reports'
    foreach ($spec in Get-ProjectTestSpecs) {
        $directory = Join-Path $rawRoot $spec.Artifact
        Write-Trx (Join-Path $directory $spec.Report)
        if ($spec.Coverage) {
            [IO.File]::WriteAllText((Join-Path $directory 'coverage.cobertura.xml'), '<coverage lines-covered="5" lines-valid="10"/>')
            $staging = Join-Path $directory 'In/staging'
            New-Item -ItemType Directory -Path $staging -Force | Out-Null
            [IO.File]::WriteAllText((Join-Path $staging 'duplicate.cobertura.xml'), '<ignored/>')
        }
    }
    # Even if a previous bundle is present, readers only consume the named raw reports.
    Write-Trx (Join-Path $rawRoot 'test-results-combined-previous/core-tests.trx')
    Assert-ReviewCondition (@(Get-ProjectTestReports $rawRoot).Count -eq 8) 'A previous summary must not duplicate project reports.'
    Assert-ReviewCondition (@(Get-TestCoverageRows $rawRoot).Count -eq 7) 'Count one canonical coverage report per project.'

    $checks = @('Install', 'Open', 'Uninstall') | ForEach-Object { @{ Name = $_; Passed = $true; Status = 'Passed' } }
    $installer = @{ SchemaVersion = 1; Suite = 'InstallerLifecycle'; Total = 3; Passed = 3; Failed = 0; Checks = @($checks) }
    $ui = @{ Status = 'Skipped'; Passed = $false; Skipped = $true; SkipReason = 'NoInteractiveDesktop'; ExpectedCount = 0; Results = @();
        Environment = @{ Available = $false; ReasonCode = 'NoInteractiveDesktop'; Reason = 'Contract fixture without UI' } }
    foreach ($spec in @(@{ Dir = 'test-results-installer'; File = 'installer-results.json'; Data = $installer },
                        @{ Dir = 'test-results-ui-operation'; File = 'ui-operation-results.json'; Data = $ui })) {
        $directory = Join-Path $rawRoot $spec.Dir
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        $spec.Data | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $directory $spec.File)
    }
    $env:GITHUB_OUTPUT = Join-Path $fixture 'summary-output'
    $env:GITHUB_STEP_SUMMARY = Join-Path $fixture 'summary.md'
    & (Join-Path $PSScriptRoot 'Summarize-TestResults.ps1') -Root $rawRoot
    Assert-ReviewCondition ((Get-Content $env:GITHUB_OUTPUT -Raw).Contains('ui_outcome=Skipped')) 'Summary must preserve the UI skip outcome.'
    $installerFile = Join-Path $rawRoot 'test-results-installer/installer-results.json'
    $headless = Resolve-UIOperationEnvironment ([pscustomobject]@{ Windows = $true; UserInteractive = $false })
    $headlessInstaller = Invoke-InstallerLifecycle -Install {} -Open { throw 'No UI in this contract.' } -Uninstall {} -Environment $headless
    $headlessInstaller | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $installerFile
    [IO.File]::WriteAllText($env:GITHUB_OUTPUT, '')
    & (Join-Path $PSScriptRoot 'Summarize-TestResults.ps1') -Root $rawRoot
    Assert-ReviewCondition ((Get-Content $env:GITHUB_OUTPUT -Raw).Contains('installer_outcome=OpenSkipped')) 'Summary must preserve installer Open skip.'
    Assert-ReviewCondition ((Get-Content $env:GITHUB_STEP_SUMMARY -Raw).Contains('**Open skipped**')) 'Summary must not claim full installation UI acceptance on a headless runner.'
    $installer | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $installerFile
    [IO.File]::WriteAllText($env:GITHUB_OUTPUT, '')
    $extraCoverage = Join-Path $rawRoot 'test-results-core/extra.cobertura.xml'
    [IO.File]::WriteAllText($extraCoverage, '<coverage lines-covered="5" lines-valid="10"/>')
    Assert-ReportThrows { & (Join-Path $PSScriptRoot 'Summarize-TestResults.ps1') -Root $rawRoot } 'Duplicate coverage in one project'
    Assert-ReviewCondition ([IO.File]::ReadAllText($env:GITHUB_OUTPUT) -eq '') 'Failed summary must not emit success outputs.'

    # Execute the real runner with a fake dotnet command in an isolated checkout.
    $runnerRoot = Join-Path $fixture 'runner'
    $runnerDirectory = Join-Path $runnerRoot 'test'
    New-Item -ItemType Directory -Path $runnerDirectory -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Run-ProjectTests.ps1'), (Join-Path $PSScriptRoot 'TestReportSupport.ps1') -Destination $runnerDirectory
    $global:ReportTestMode = 'pass'
    function dotnet {
        $global:ReportTestArguments = @($args)
        $global:ReportTestBin = $env:RELEASE_BIN_DIR
        $global:LASTEXITCODE = if ($global:ReportTestMode -eq 'exit-failure') { 9 } else { 0 }
        $logger = $args[([array]::IndexOf($args, '--logger') + 1)]
        $directory = $args[([array]::IndexOf($args, '--results-directory') + 1)]
        if ($global:ReportTestMode -ne 'missing-report') {
            $reportPath = Join-Path $PWD.Path "$directory/$($logger.Substring('trx;LogFileName='.Length))"
            Write-Trx $reportPath -Outcome $(if ($global:ReportTestMode -eq 'failed-report') { 'Failed' } else { 'Completed' })
        }
    }
    $runner = Join-Path $runnerDirectory 'Run-ProjectTests.ps1'
    $env:GITHUB_OUTPUT = Join-Path $fixture 'runner-output'
    & $runner -JobId test-core
    Assert-ReviewCondition ($global:ReportTestArguments -contains '--no-build' -and $global:ReportTestArguments -contains 'src/test.runsettings') 'Core must reuse the build and shared coverage settings.'
    Assert-ReportThrows { & $runner -JobId test-core } 'Stale report reuse'
    & $runner -JobId test-ml
    Assert-ReviewCondition ($global:ReportTestArguments -contains 'TestCategory!=Integration') 'ML integration is opt-in.'
    Remove-Item -LiteralPath (Join-Path $runnerRoot 'TestResults/ML/ml-tests.trx')
    & $runner -JobId test-ml -IncludeMLIntegration
    Assert-ReviewCondition ($global:ReportTestArguments -notcontains '--filter') 'Explicit ML integration must not be filtered out.'
    & $runner -JobId test-artifacts
    Assert-ReviewCondition ($global:ReportTestArguments -notcontains '--settings' -and $global:ReportTestBin -eq (Join-Path $runnerRoot 'src/VirtualPaper/bin/Release/net8.0-windows10.0.19041.0')) 'Artifact checks need the build directory and no coverage collector.'
    foreach ($case in @(@('exit-failure', 'test-shader'), @('missing-report', 'test-images'), @('failed-report', 'test-web-backdrop'))) {
        $global:ReportTestMode = $case[0]
        Assert-ReportThrows { & $runner -JobId $case[1] } $case[0]
    }
    Assert-ReportThrows { & $runner -JobId unknown } 'Unknown project'
    Write-Host '[OK] TRX validity, report isolation on retry, per-project coverage, explicit UI skips and shared runner failure propagation.'
} finally {
    Remove-Item Function:\dotnet -ErrorAction SilentlyContinue
    Remove-Variable ReportTestArguments, ReportTestMode, ReportTestBin -Scope Global -ErrorAction SilentlyContinue
    $env:GITHUB_OUTPUT = $oldOutput; $env:GITHUB_STEP_SUMMARY = $oldSummary
    $resolved = [IO.Path]::GetFullPath($fixture)
    if ([IO.Path]::GetDirectoryName($resolved) -ne $resultsRoot) { throw 'Unsafe report test cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
