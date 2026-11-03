#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $InstallerPath,
    [string] $ReportPath = 'TestResults/Smoke/smoke-results.json',
    [ValidateSet('Debug', 'Release')][string] $Configuration = 'Release',
    [ValidateRange(10, 600)][int] $TimeoutSeconds = 120,
    [switch] $NoBuild
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/SmokeTestSupport.ps1"
if (-not $IsWindows) { throw 'Installer smoke tests require Windows.' }
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$InstallerPath = (Resolve-Path -LiteralPath $InstallerPath).Path
$ReportPath = [IO.Path]::GetFullPath($ReportPath)
if (Test-Path -LiteralPath $ReportPath) { throw "Refusing to reuse an existing smoke report: $ReportPath" }
$artifacts = "$ReportPath.artifacts"
if (Test-Path -LiteralPath $artifacts) { throw "Smoke artifacts already exist: $artifacts" }
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
# Unicode and spaces exercise installer/application path handling; never reuse a daily install.
$installDir = Join-Path ([IO.Path]::GetTempPath()) "Virtual Paper 冒烟测试-$([guid]::NewGuid().ToString('N'))"
$project = Join-Path $repositoryRoot 'src/VirtualPaper.SmokeTest/VirtualPaper.SmokeTest.csproj'
$probe = Join-Path $repositoryRoot "src/VirtualPaper.SmokeTest/bin/$Configuration/net8.0-windows10.0.19041.0/VirtualPaper.SmokeTest.dll"
if (-not $NoBuild) {
    dotnet build $project -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Smoke probe build failed; installation was not attempted.' }
}
if (-not (Test-Path -LiteralPath $probe)) { throw "Smoke probe not found: $probe" }

function Invoke-BoundedProcess([string] $Executable, [string] $Arguments) {
    $process = Start-Process -FilePath $Executable -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    try {
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw "Process timed out: $Executable"
        }
        if ($process.ExitCode -ne 0) { throw "Process exited with $($process.ExitCode): $Executable" }
    } finally { $process.Dispose() }
}

function Get-SmokeProcesses {
    $prefix = [IO.Path]::GetFullPath($installDir).TrimEnd('\') + '\'
    foreach ($process in Get-Process -Name 'VirtualPaper*' -ErrorAction SilentlyContinue) {
        try {
            $path = $process.Path
            if ($path -and $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { $process }
        } catch [System.ComponentModel.Win32Exception] { }
    }
}

$report = Invoke-SmokeLifecycle -Install {
    Invoke-BoundedProcess $InstallerPath "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOAUTOLAUNCH /DIR=`"$installDir`" /LOG=`"$artifacts/install.log`""
    if (-not (Test-Path -LiteralPath (Join-Path $installDir 'VirtualPaper.exe'))) { throw 'Installed executable is missing.' }
} -Launch {
    $launchReport = Join-Path $artifacts 'launch-results.json'
    Invoke-BoundedProcess (Get-Command dotnet).Source "`"$probe`" --install-dir `"$installDir`" --timeout-seconds $([math]::Max(1, $TimeoutSeconds - 20)) --results-file `"$launchReport`""
    $launch = Get-Content -LiteralPath $launchReport -Raw | ConvertFrom-Json
    if ($launch.Total -ne 1 -or $launch.Passed -ne 1 -or $launch.Failed -ne 0 -or
        @($launch.Checks).Count -ne 1 -or $launch.Checks[0].Name -ne 'Launch' -or $launch.Checks[0].Passed -cne $true) {
        throw 'Installed application did not pass its launch probe.'
    }
} -Uninstall {
    # Also reap this isolated install after a launch timeout, before running its uninstaller.
    foreach ($process in @(Get-SmokeProcesses)) {
        if (-not $process.HasExited) {
            $process.Kill($true)
            if (-not $process.WaitForExit(10000)) { throw "Smoke process did not stop: $($process.Id)" }
        }
    }
    if (Test-Path -LiteralPath $installDir) {
        $uninstallers = @(Get-ChildItem -LiteralPath $installDir -Filter 'unins*.exe' -File)
        if ($uninstallers.Count -ne 1) { throw "Expected one uninstaller, found $($uninstallers.Count)." }
        Invoke-BoundedProcess $uninstallers[0].FullName "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=`"$artifacts/uninstall.log`""
        # Inno's temporary uninstaller helper may finish deleting files just after the parent exits.
        $cleanup = [Diagnostics.Stopwatch]::StartNew()
        do {
            $files = if (Test-Path -LiteralPath $installDir) {
                @(Get-ChildItem -LiteralPath $installDir -Recurse -File -Force)
            } else { @() }
            $files = @($files)
            if ($files.Count -eq 0) { break }
            Start-Sleep -Milliseconds 200
        } while ($cleanup.Elapsed.TotalSeconds -lt 10)
        if ($files.Count -gt 0) { throw "Files remain after uninstall: $($files.FullName -join '; ')" }
    }
    if (@(Get-SmokeProcesses).Count -ne 0) { throw 'Smoke processes remain after uninstall.' }
}
$report | Add-Member NoteProperty InstallDir $installDir
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $ReportPath -Encoding utf8
if ($env:GITHUB_STEP_SUMMARY) {
    "### Smoke: install → launch → uninstall`n`n| Stage | Result | Duration |`n| --- | --- | ---: |" >> $env:GITHUB_STEP_SUMMARY
    foreach ($check in $report.Checks) {
        "| $($check.Name) | $($check.Status) | $($check.DurationMilliseconds) ms |" >> $env:GITHUB_STEP_SUMMARY
    }
}
Assert-SmokeReportPassed $report
Write-Host "[OK] Install -> launch -> uninstall. Report: $ReportPath"
