#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $InstallerPath,
    [string] $ReportPath = 'TestResults/Installer/installer-results.json',
    [ValidateSet('english', 'chinese')][string] $InstallerLanguage = 'english',
    [ValidateSet('Debug', 'Release')][string] $Configuration = 'Release',
    [ValidateRange(10, 600)][int] $TimeoutSeconds = 120,
    [switch] $RunUIOperationTests,
    [string] $UIHostExe,
    [string] $UIReportPath = 'TestResults/UIOperation/ui-operation-results.json',
    [ValidateRange(30, 900)][int] $UITimeoutSeconds = 900,
    [switch] $NoBuild
)
$ErrorActionPreference = 'Stop'
$InstallerLanguage = $InstallerLanguage.ToLowerInvariant()
. "$PSScriptRoot/InstallerTestSupport.ps1"
if (-not $IsWindows) { throw 'Installer lifecycle tests require Windows.' }
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$InstallerPath = (Resolve-Path -LiteralPath $InstallerPath).Path
$ReportPath = [IO.Path]::GetFullPath($ReportPath)
if (Test-Path -LiteralPath $ReportPath) { throw "Refusing to reuse an existing installer lifecycle report: $ReportPath" }
$artifacts = "$ReportPath.artifacts"
if (Test-Path -LiteralPath $artifacts) { throw "Installer lifecycle artifacts already exist: $artifacts" }
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
# Spaces exercise installer/application path handling; never reuse a daily install.
$installDir = Join-Path ([IO.Path]::GetTempPath()) "Virtual Paper install-acceptance-$([guid]::NewGuid().ToString('N'))"
$project = Join-Path $repositoryRoot 'test/VirtualPaper.InstallerTest/VirtualPaper.InstallerTest.csproj'
$probe = Join-Path $repositoryRoot "test/VirtualPaper.InstallerTest/bin/$Configuration/net8.0-windows10.0.19041.0/VirtualPaper.InstallerTest.dll"
if (-not $NoBuild) {
    dotnet build $project -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Installer lifecycle probe build failed; installation was not attempted.' }
}
if (-not (Test-Path -LiteralPath $probe)) { throw "Installer lifecycle probe not found: $probe" }
if ($RunUIOperationTests) {
    if (-not $UIHostExe -or -not (Test-Path -LiteralPath $UIHostExe -PathType Leaf)) {
        throw 'Installed UI acceptance requires a prebuilt -UIHostExe.'
    }
    $UIHostExe = [IO.Path]::GetFullPath($UIHostExe)
    $UIReportPath = [IO.Path]::GetFullPath($UIReportPath)
    if (Test-Path -LiteralPath $UIReportPath) { throw "UI report already exists: $UIReportPath" }
    if (Test-Path -LiteralPath "$UIReportPath.artifacts") { throw "UI artifacts already exist: $UIReportPath.artifacts" }
    if ($UIReportPath -eq $ReportPath -or $UIReportPath.StartsWith("$artifacts\", [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Installer and UI reports must use separate artifact paths.'
    }
}

# The production AppId is shared with a user's existing installation. Read all registry
# views before installing, so this acceptance run cannot overwrite a daily installation.
$setupScriptPath = Join-Path $repositoryRoot 'InnoSetup/setup.iss'
Assert-CleanInstallerEnvironment $setupScriptPath
$appId = Get-InstallerAppId $setupScriptPath
$shortcutPaths = @(Get-InstallerShortcutPaths)
$packageVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($InstallerPath).FileVersion
$installedVersion = $null
$environment = Get-UIOperationEnvironment
$mode = if ($environment.Available) { 'InteractiveWizard' } else { 'Silent' }
Write-Host "Installer acceptance mode: $mode"
$uiOperation = $null
if ($RunUIOperationTests) {
    if (-not $environment.Available) {
        # Preserve the same read-only preflight evidence as the skipped startup
        # stage. Do not try to launch UI or retest the desktop after uninstall.
        New-Item -ItemType Directory -Path "$UIReportPath.artifacts" -Force | Out-Null
        $environment | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$UIReportPath.artifacts/environment.json" -Encoding utf8
        New-UIOperationSkipReport $environment | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $UIReportPath -Encoding utf8
    }
    $uiOperation = {
        $installedCore = Join-Path $installDir 'VirtualPaper.exe'
        & "$PSScriptRoot/Run-UIOperationTests.ps1" -Configuration $Configuration -NoBuild -Suite Extended `
            -CoreExe $installedCore -HostExe $UIHostExe -ReportPath $UIReportPath -TimeoutSeconds $UITimeoutSeconds
        # The runner checks its process exit code and throws on failure. As a
        # PowerShell script it need not set LASTEXITCODE (which may be stale/null).
        $ui = Get-Content -LiteralPath $UIReportPath -Raw | ConvertFrom-Json
        if ((Get-UIOperationReportOutcome $ui) -ne 'Passed') {
            throw 'The desktop became unavailable after startup; installed UI acceptance did not run.'
        }
    }
}

function Invoke-BoundedProcess([string] $Executable, [string] $Arguments) {
    $process = Start-Process -FilePath $Executable -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    try {
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $process.WaitForExit(10000) | Out-Null
            throw "Process timed out: $Executable"
        }
        if ($process.ExitCode -ne 0) { throw "Process exited with $($process.ExitCode): $Executable" }
    } finally { $process.Dispose() }
}

function Get-InstallerTestProcesses {
    $prefix = [IO.Path]::GetFullPath($installDir).TrimEnd('\') + '\'
    foreach ($process in Get-Process -Name 'VirtualPaper*' -ErrorAction SilentlyContinue) {
        try {
            $path = $process.Path
            if ($path -and $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { $process }
        } catch [System.ComponentModel.Win32Exception] { }
    }
}

function Invoke-InstallerWizard([string] $Executable, [switch] $Uninstall) {
    $action = if ($Uninstall) { '--wizard-uninstall' } else { '--wizard-install' }
    $stage = if ($Uninstall) { 'uninstall' } else { 'install' }
    Invoke-BoundedProcess (Get-Command dotnet).Source "`"$probe`" $action `"$Executable`" --language $InstallerLanguage --install-dir `"$installDir`" --timeout-seconds $([math]::Max(1, $TimeoutSeconds - 10)) --results-file `"$artifacts/$stage-wizard.json`""
}

$report = Invoke-InstallerLifecycle -Install {
    if ($environment.Available) { Invoke-InstallerWizard $InstallerPath }
    else { Invoke-BoundedProcess $InstallerPath "/LANG=$InstallerLanguage /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOAUTOLAUNCH /DIR=`"$installDir`" /LOG=`"$artifacts/install.log`"" }
    $languageMarker = Join-Path $installDir 'installer-language.txt'
    if (-not (Test-Path -LiteralPath $languageMarker) -or
        [IO.File]::ReadAllText($languageMarker).Trim() -cne $InstallerLanguage) {
        throw 'The installed language marker does not match the selected installer language.'
    }
    if (@(Get-ChildItem -LiteralPath $installDir -Directory -Recurse -Filter '*.WebView2').Count) {
        throw 'The installer contains WebView2 user data from the build machine.'
    }
    foreach ($file in @('VirtualPaper.exe', 'VirtualPaper.dll', 'VirtualPaper.runtimeconfig.json', 'VirtualPaper.deps.json',
        'VirtualPaper.Common.dll', 'Plugins/UI/VirtualPaper.UI.exe', 'Plugins/UI/VirtualPaper.UI.dll',
        'Plugins/UI/VirtualPaper.UI.runtimeconfig.json', 'Plugins/UI/VirtualPaper.UI.deps.json')) {
        $path = Join-Path $installDir $file
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) {
            throw "Installed file is missing or empty: $file"
        }
    }
    $script:installedVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $installDir 'VirtualPaper.exe')).FileVersion
    if ([version]$installedVersion -ne [version]$packageVersion) { throw "Installed version $installedVersion does not match package $packageVersion." }
    $registrations = @(Get-InstallRegistrations $appId)
    if ($registrations.Count -eq 0 -or @($registrations | Where-Object {
        -not $_.Location -or [IO.Path]::GetFullPath($_.Location).TrimEnd('\') -ine $installDir.TrimEnd('\') -or
        [version]$_.Version -ne [version]$packageVersion
    }).Count) { throw 'The installed application registration does not match this installation.' }
    $shortcuts = @($shortcutPaths | Where-Object { Test-Path -LiteralPath $_ })
    if ($shortcuts.Count -eq 0) { throw "The Start menu shortcut was not installed. Checked: $($shortcutPaths -join '; ')" }
    $expectedTarget = Join-Path $installDir 'VirtualPaper.exe'
    $shell = New-Object -ComObject WScript.Shell
    try {
        foreach ($path in $shortcuts) {
            $shortcut = $shell.CreateShortcut($path) # Read-only: never Save().
            try {
                $actualTarget = $shortcut.TargetPath
                $targetExists = -not [string]::IsNullOrWhiteSpace($actualTarget) -and
                    (Test-Path -LiteralPath $actualTarget -PathType Leaf)
                Write-Host ('Start menu shortcut: ' + ([ordered]@{
                    ShortcutPath = $path; ActualTarget = $actualTarget
                    ExpectedTarget = $expectedTarget; TargetExists = $targetExists
                } | ConvertTo-Json -Compress))
                if ([string]::IsNullOrWhiteSpace($actualTarget)) {
                    throw "Start menu shortcut has an empty target: $path. Expected: $expectedTarget"
                }
                if ($actualTarget -ine $expectedTarget) {
                    throw "Start menu shortcut points to the wrong application: $path. Actual: $actualTarget; expected: $expectedTarget; target exists: $targetExists"
                }
                if (-not $targetExists) { throw "Start menu shortcut target does not exist: $actualTarget (shortcut: $path)" }
            } finally { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) | Out-Null }
        }
    } finally { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null }
} -Open {
    $openReport = Join-Path $artifacts 'application-results.json'
    Invoke-BoundedProcess (Get-Command dotnet).Source "`"$probe`" --install-dir `"$installDir`" --timeout-seconds $([math]::Max(1, $TimeoutSeconds - 40)) --results-file `"$openReport`""
    $application = Get-Content -LiteralPath $openReport -Raw | ConvertFrom-Json
    if ($application.Passed -cne $true -or $application.OpenedNormally -cne $true -or
        $application.ExitedNormally -cne $true -or $application.ForcedCleanup -cne $false -or
        $application.CoreProcessId -lt 1 -or $application.UiProcessId -lt 1 -or $application.LanguageVerified -cne $true -or $application.Error) {
        throw 'Installed application did not open and exit normally.'
    }
} -Uninstall {
    $cleanupErrors = [Collections.Generic.List[string]]::new()
    foreach ($process in @(Get-InstallerTestProcesses)) {
        try {
            if (-not $process.HasExited) {
                $cleanupErrors.Add("Application process $($process.Id) required forced recovery; normal exit was not accepted.")
                $process.Kill($true)
                if (-not $process.WaitForExit(10000)) { throw "Process did not stop: $($process.Id)" }
            }
        } catch { $cleanupErrors.Add($_.Exception.Message) }
        finally { $process.Dispose() }
    }
    # Always try the actual uninstaller after recovery, including when terminating a process failed.
    if (Test-Path -LiteralPath $installDir) {
        $uninstallers = @(Get-ChildItem -LiteralPath $installDir -Filter 'unins*.exe' -File)
        if ($uninstallers.Count -ne 1) { throw "Expected one uninstaller, found $($uninstallers.Count)." }
        if ($environment.Available) { Invoke-InstallerWizard $uninstallers[0].FullName -Uninstall }
        else { Invoke-BoundedProcess $uninstallers[0].FullName "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=`"$artifacts/uninstall.log`"" }
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
    if (@(Get-InstallerTestProcesses).Count -ne 0) { $cleanupErrors.Add('Application processes remain after uninstall.') }
    if (@(Get-InstallRegistrations $appId).Count) { $cleanupErrors.Add('Application uninstall registration remains after uninstall.') }
    if (@($shortcutPaths | Where-Object { Test-Path -LiteralPath $_ }).Count) { $cleanupErrors.Add('Start menu shortcut remains after uninstall.') }
    if ($cleanupErrors.Count) { throw ($cleanupErrors -join [Environment]::NewLine) }
} -UIOperation $uiOperation -Environment $environment
$report | Add-Member NoteProperty InstallDir $installDir
$report | Add-Member NoteProperty Mode $mode
$report | Add-Member NoteProperty PackageVersion $packageVersion
$report | Add-Member NoteProperty InstalledVersion $installedVersion
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $ReportPath -Encoding utf8
if ($env:GITHUB_STEP_SUMMARY) {
    $sequence = $report.Checks.Name -join ' → '
    "### Installed application: $sequence`n`n| Stage | Result | Duration |`n| --- | --- | ---: |" >> $env:GITHUB_STEP_SUMMARY
    foreach ($check in $report.Checks) {
        "| $($check.Name) | $($check.Status) | $($check.DurationMilliseconds) ms |" >> $env:GITHUB_STEP_SUMMARY
    }
}
Assert-InstallerReportPassed $report
Write-Host "[OK] Installer acceptance: $(Get-InstallerReportOutcome $report). Report: $ReportPath"
