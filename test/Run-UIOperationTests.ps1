#requires -Version 7.2
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [string]$ReportPath = (Join-Path ([IO.Path]::GetTempPath()) ('virtualpaper-ui-operation-' + [guid]::NewGuid().ToString('N') + '.json')),
    [string]$CoreExe,
    [string]$HostExe,
    [string]$PackageFullName,
    [string]$CIOwnershipPath,
    [string]$BuiltInPayload,
    [string]$DataRoot,
    [ValidatePattern('^[A-Za-z][A-Za-z0-9]*(\.[A-Za-z][A-Za-z0-9]*)*$')][string]$Module,
    [ValidateSet('Standard', 'Extended')][string]$Suite = 'Standard',
    [ValidateRange(30, 900)][int]$TimeoutSeconds = 180,
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
if ($Module -and $Suite -ne 'Standard') { throw 'Choose either a single module or an extended suite.' }
if ($DataRoot -and -not $PackageFullName) { throw 'An external retained DataRoot is only supported for installed MSIX acceptance.' }
. (Join-Path $PSScriptRoot 'PackageArtifactSupport.ps1')
$ciArtifact = $null
if ($CIOwnershipPath) {
    if (-not $PackageFullName) { throw 'CI ownership requires installed package activation.' }
    $ciArtifact = Assert-MsixCIOwnership $CIOwnershipPath $PackageFullName
}
if ($PackageFullName -and -not $ciArtifact -and $PackageFullName -notmatch '^PaperHammer\.VirtualPaper\.Test\.[0-9a-f]{16}_') {
    throw 'MSIX UI acceptance requires a per-run test identity from Run-MsixTests.ps1; daily Store/preview packages cannot be used.'
}
if ($DataRoot) {
    $DataRoot = [IO.Path]::GetFullPath($DataRoot)
    $relativeRoot = [IO.Path]::GetRelativePath([IO.Path]::GetTempPath(), $DataRoot)
    if ($relativeRoot -notmatch '^VirtualPaper\.MsixAcceptance-[0-9a-f]{32}[\\/].+') {
        throw 'Retained MSIX profiles must be inside a Run-MsixTests.ps1 temporary profile directory.'
    }
}
if ($Suite -eq 'Extended' -and -not $PSBoundParameters.ContainsKey('TimeoutSeconds')) { $TimeoutSeconds = 900 }
. (Join-Path $PSScriptRoot 'UIOperationTestSupport.ps1')
$projectPath = Join-Path $PSScriptRoot 'VirtualPaper.UIOperationTest/VirtualPaper.UIOperationTest.csproj'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$reportFullPath = [IO.Path]::GetFullPath($ReportPath)
if (Test-Path -LiteralPath $reportFullPath) { throw 'Report path must be new to avoid reading stale results' }
$artifactPath = $reportFullPath + '.artifacts'
if (Test-Path -LiteralPath $artifactPath) { throw 'Artifact directory must be new to avoid mixing runs' }
New-Item -ItemType Directory -Path $artifactPath -Force | Out-Null
$environment = Get-UIOperationEnvironment
$environment | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $artifactPath 'environment.json') -Encoding utf8
if (-not $environment.Available) {
    New-UIOperationSkipReport $environment | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportFullPath -Encoding utf8
    Write-Warning "WinUI acceptance SKIPPED [$($environment.ReasonCode)]: $($environment.Reason)"
    Write-Output "Report: $reportFullPath"
    Write-Output "Artifacts: $artifactPath"
    exit 0
}
if (-not $NoBuild) {
    $msbuild = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    $msbuildPath = if ($msbuild) { $msbuild.Source } else {
        & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
    }
    if (-not $msbuildPath) { throw 'Visual Studio MSBuild is required to build Core, UI and the test host' }
    $buildLog = Join-Path $artifactPath 'build.log'
    & $msbuildPath (Join-Path $repositoryRoot 'src/VirtualPaper.sln') /t:Build /restore "/p:Configuration=$Configuration" '/p:Platform=Any CPU' /m /v:minimal /nologo *> $buildLog
    if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $buildLog -Tail 25; throw 'Application/test build failed' }
}
if ($PackageFullName) {
    if ($CoreExe) { throw 'Choose installed package activation or an unpackaged Core executable.' }
    $installed = @(Get-AppxPackage | Where-Object PackageFullName -CEQ $PackageFullName)
    if ($installed.Count -ne 1) { throw 'The exact MSIX package must be installed for the current user.' }
    if ($ciArtifact -and ($installed[0].Name -cne $ciArtifact.IdentityName -or $installed[0].Publisher -cne $ciArtifact.Publisher -or
        $installed[0].Version.ToString() -cne $ciArtifact.PackageVersion)) { throw 'Installed CI package differs from its artifact.' }
    $CoreExe = Join-Path $installed[0].InstallLocation 'VirtualPaper.exe'
}
if (-not $CoreExe) { $CoreExe = Join-Path $repositoryRoot "src/VirtualPaper/bin/$Configuration/net8.0-windows10.0.19041.0/VirtualPaper.exe" }
$CoreExe = [IO.Path]::GetFullPath($CoreExe)
if (-not (Test-Path -LiteralPath $CoreExe)) { throw "Core executable not found: $CoreExe" }
if (-not (Test-Path -LiteralPath (Join-Path (Split-Path -Parent $CoreExe) 'Plugins/UI/VirtualPaper.UI.exe'))) {
    throw 'Production UI executable is missing next to Core'
}
if ($HostExe) {
    $targetExe = [IO.Path]::GetFullPath($HostExe)
} else {
    $targetDll = (dotnet msbuild $projectPath -getProperty:TargetPath "-p:Configuration=$Configuration" -p:Platform=x64 | Select-Object -Last 1).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot locate UI operation test host' }
    $targetExe = [IO.Path]::ChangeExtension($targetDll, '.exe')
}
if (-not (Test-Path -LiteralPath $targetExe)) { throw "Host not found: $targetExe" }
$hostCommonPath = Join-Path (Split-Path -Parent $targetExe) 'VirtualPaper.Common.dll'
$hostCommonHash = (Get-FileHash -LiteralPath $hostCommonPath -Algorithm SHA256).Hash
foreach ($applicationDirectory in @((Split-Path -Parent $CoreExe), (Join-Path (Split-Path -Parent $CoreExe) 'Plugins/UI'))) {
    $applicationCommonPath = Join-Path $applicationDirectory 'VirtualPaper.Common.dll'
    if ((Get-FileHash -LiteralPath $applicationCommonPath -Algorithm SHA256).Hash -ne $hostCommonHash) {
        throw "Application and test isolation binaries differ; rebuild the complete solution: $applicationCommonPath"
    }
}
$startInfo = [Diagnostics.ProcessStartInfo]::new($targetExe)
$startInfo.UseShellExecute = $false
$startInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$startInfo.WorkingDirectory = [IO.Path]::GetDirectoryName($targetExe)
$startInfo.ArgumentList.Add('--report')
$startInfo.ArgumentList.Add($reportFullPath)
if ($Module) {
    $startInfo.ArgumentList.Add('--module')
    $startInfo.ArgumentList.Add($Module)
} elseif ($Suite -eq 'Extended') {
    $startInfo.ArgumentList.Add('--suite')
    $startInfo.ArgumentList.Add('Extended')
}
$sessionId = [guid]::NewGuid().ToString('N')
# Keep WebView2 media/cache paths below native MAX_PATH even under a long TEMP.
$sessionRoot = if ($DataRoot) { [IO.Path]::GetFullPath($DataRoot) } else {
    [IO.Path]::GetFullPath((Join-Path $env:TEMP ('VPUI-' + $sessionId)))
}
New-Item -ItemType Directory -Path $sessionRoot -Force | Out-Null
$startInfo.Environment['VIRTUALPAPER_TEST_SESSION'] = $sessionId
$startInfo.Environment['VIRTUALPAPER_TEST_DATA_ROOT'] = $sessionRoot
$startInfo.Environment['VIRTUALPAPER_TEST_CORE_EXE'] = $CoreExe
if ($BuiltInPayload) {
    $BuiltInPayload = [IO.Path]::GetFullPath($BuiltInPayload)
    if (-not (Test-Path -LiteralPath $BuiltInPayload -PathType Container)) { throw 'Installed built-in wallpaper fixture is missing.' }
    $startInfo.Environment['VIRTUALPAPER_TEST_BUILTIN_PAYLOAD'] = $BuiltInPayload
} else { [void]$startInfo.Environment.Remove('VIRTUALPAPER_TEST_BUILTIN_PAYLOAD') }
if ($PackageFullName) {
    $startInfo.Environment['VIRTUALPAPER_TEST_PACKAGE_FULL_NAME'] = $PackageFullName
    $startInfo.Environment['VIRTUALPAPER_TEST_PACKAGE_AUMID'] = "$($installed[0].PackageFamilyName)!App"
} else {
    [void]$startInfo.Environment.Remove('VIRTUALPAPER_TEST_PACKAGE_FULL_NAME')
    [void]$startInfo.Environment.Remove('VIRTUALPAPER_TEST_PACKAGE_AUMID')
}
$process = $null
try {
    $process = [Diagnostics.Process]::Start($startInfo)
    $runTimer = [Diagnostics.Stopwatch]::StartNew()
    while (-not $process.WaitForExit(1000)) {
        if ($runTimer.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
            $process.Kill($true)
            throw "UI operation tests timed out after $TimeoutSeconds seconds"
        }
    }
    if (-not (Test-Path -LiteralPath $reportFullPath)) { throw "Host exited without a report (exit $($process.ExitCode))" }
    $report = Get-Content -LiteralPath $reportFullPath -Raw | ConvertFrom-Json
    if ($Suite -eq 'Extended') {
        foreach ($moduleName in @('ModelInference', 'ScreenSaverLifecycle', 'Rendering')) {
            if (@($report.Results | Where-Object { $_.Name -like "Application.$moduleName.*" }).Count -lt 1) {
                throw "Extended acceptance is missing the $moduleName case."
            }
        }
    }
    $applicationRuns = @($report.Results | Where-Object { $_.Name -like 'Application.*' })
    if ($process.ExitCode -ne 0 -or (Get-UIOperationReportOutcome $report) -ne 'Passed') {
        throw "UI operation tests failed: $($report | ConvertTo-Json -Depth 5)"
    }
    if ($PackageFullName -and @($applicationRuns | Where-Object {
        $_.Evidence.PackageFullName -cne $PackageFullName -or $_.Evidence.PackageActivation -cne 'AUMID'
    }).Count) { throw 'MSIX UI acceptance did not retain verified package identity and AUMID activation.' }
    $report.Results | Format-Table Name, Passed, DurationMilliseconds
    foreach ($applicationRun in $applicationRuns) {
        if (@($applicationRun.Evidence.RuntimeErrors).Count -gt 0) {
            Write-Warning "$($applicationRun.Name): runtime error logs were captured; see report Evidence.RuntimeErrors and application logs."
        }
    }
    Write-Output "Report: $reportFullPath"
}
finally {
    if ($null -ne $process) {
        if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit(5000) | Out-Null }
        $process.Dispose()
    }
    # Preserve only this isolated run's logs/config for diagnostics, then remove its temporary root.
    if (Test-Path -LiteralPath (Join-Path $sessionRoot 'logs')) {
        Copy-Item -LiteralPath (Join-Path $sessionRoot 'logs') -Destination $artifactPath -Recurse
    }
    if (Test-Path -LiteralPath (Join-Path $sessionRoot 'user_settings.json')) {
        Copy-Item -LiteralPath (Join-Path $sessionRoot 'user_settings.json') -Destination $artifactPath
    }
    if (-not $DataRoot) {
        $expectedRoot = [IO.Path]::GetFullPath((Join-Path $env:TEMP ('VPUI-' + $sessionId)))
        if ($sessionRoot -ne $expectedRoot -or -not $sessionRoot.StartsWith([IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Unexpected test session cleanup path'
        }
        # WebView2 can release its cache after the player/Core has exited. Wait for
        # that release instead of touching any unrelated browser or application.
        $cleanupTimer = [Diagnostics.Stopwatch]::StartNew()
        while (Test-Path -LiteralPath $sessionRoot) {
            try { Remove-Item -LiteralPath $sessionRoot -Recurse -Force; break }
            catch [IO.IOException] {
                if ($cleanupTimer.Elapsed.TotalSeconds -ge 15) { throw }
                Start-Sleep -Milliseconds 200
            }
        }
    }
    Write-Output "Artifacts: $artifactPath"
}
