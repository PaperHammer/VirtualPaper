#requires -Version 7.2
param(
    [string]$Configuration = 'Debug',
    [string]$ReportPath = (Join-Path $env:TEMP ('virtualpaper-effect-ui-' + [guid]::NewGuid().ToString('N') + '.json'))
)
$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot 'VirtualPaper.Sandbox.WinUI.Preview.csproj'
$buildLog = Join-Path $env:TEMP 'virtualpaper-effect-ui-build.log'
dotnet build $projectPath -c $Configuration --no-restore -p:Platform=x64 -p:WindowsPackageType=None --verbosity quiet *> $buildLog
if ($LASTEXITCODE -ne 0) {
    Get-Content -LiteralPath $buildLog -Tail 20
    throw 'Component workflow host build failed'
}
$targetDll = (dotnet msbuild $projectPath -getProperty:TargetPath "-p:Configuration=$Configuration" -p:Platform=x64 -p:WindowsPackageType=None | Select-Object -Last 1).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot locate component workflow host' }
$targetExe = [System.IO.Path]::ChangeExtension($targetDll, '.exe')
if (-not (Test-Path -LiteralPath $targetExe)) { throw "Host not found: $targetExe" }
$resolvedReport = [System.IO.Path]::GetFullPath($ReportPath)
if (Test-Path -LiteralPath $resolvedReport) { throw 'Report path must be new to avoid reading stale results' }
$startInfo = [System.Diagnostics.ProcessStartInfo]::new($targetExe)
$startInfo.UseShellExecute = $false
$startInfo.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
$startInfo.WorkingDirectory = [System.IO.Path]::GetDirectoryName($targetExe)
$startInfo.ArgumentList.Add('--verify-general-effect')
$startInfo.ArgumentList.Add($resolvedReport)
$process = [System.Diagnostics.Process]::Start($startInfo)
try {
    if (-not $process.WaitForExit(30000)) {
        $process.Kill($true)
        throw 'Component workflow timed out after 30 seconds'
    }
    if (-not (Test-Path -LiteralPath $resolvedReport)) { throw "Host exited without a report (exit $($process.ExitCode))" }
    $report = Get-Content -Raw -LiteralPath $resolvedReport | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $report.passed) { throw "Component workflow failed: $($report.error)" }
    $report
    Write-Output "Report: $resolvedReport"
}
finally { $process.Dispose() }
