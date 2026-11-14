#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('EXE', 'MSIX')][string] $Channel,
    [Parameter(Mandatory)][string] $PreviousArtifact,
    [Parameter(Mandatory)][string] $PackageArtifact,
    [Parameter(Mandatory)][string] $PreviousTestRoot,
    [string] $TestRoot = (Split-Path -Parent $PSScriptRoot),
    [string] $ResultsRoot = "TestResults/upgrade-$Channel-$(Get-Date -Format 'yyyyMMdd-HHmmss')",
    [ValidateRange(30, 600)][int] $TimeoutSeconds = 240
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/InstallerTestSupport.ps1"
. "$PSScriptRoot/MsixTestSupport.ps1"
. "$PSScriptRoot/BuiltInWallpaperTestSupport.ps1"
. "$PSScriptRoot/UpgradeTestSupport.ps1"
if (-not $IsWindows) { throw 'Upgrade acceptance requires Windows.' }
$pair = Get-UpgradePackagePair $PreviousArtifact $PackageArtifact $Channel
$previous = $pair.Previous
$next = $pair.Next
$PreviousTestRoot = (Resolve-Path -LiteralPath $PreviousTestRoot).Path
$TestRoot = (Resolve-Path -LiteralPath $TestRoot).Path
$PreviousArtifact = (Resolve-Path -LiteralPath $PreviousArtifact).Path
$PackageArtifact = (Resolve-Path -LiteralPath $PackageArtifact).Path
$ResultsRoot = [IO.Path]::GetFullPath($ResultsRoot)
if (Test-Path -LiteralPath $ResultsRoot) { throw 'Upgrade results require a fresh directory.' }
$environment = Get-UIOperationEnvironment
if (-not $environment.Available) { throw "Required upgrade UI is unavailable: $($environment.Reason)" }
if ($Channel -eq 'EXE') { Assert-CleanInstallerEnvironment (Join-Path $TestRoot 'packaging/inno/setup.iss') }
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Upgrade acceptance requires elevated PowerShell.' }
[void](New-Item -ItemType Directory -Path $ResultsRoot)
$runId = [guid]::NewGuid().ToString('N')
$profileFolder = if ($Channel -eq 'MSIX') { "VirtualPaper.MsixAcceptance-$runId" } else { "VPUpgrade-$runId" }
$profiles = Join-Path ([IO.Path]::GetTempPath()) $profileFolder
$profile = Join-Path $profiles 'retained'
$installDir = Join-Path $profiles 'application'
$reportPath = Join-Path $ResultsRoot 'upgrade-results.json'
$report = [ordered]@{ Suite = 'CrossVersionUpgrade'; Channel = $Channel; Passed = $false
    Previous = $previous; Next = $next; Environment = $environment; Stages = @(); Error = $null }
$installed = $null
$builtIns = $null
$installationAttempted = $false
$trusted = $false
$certificate = $null

function Invoke-UpgradeStage([string] $Name, [scriptblock] $Action) {
    $stage = [ordered]@{ Name = $Name; Status = 'Failed'; Error = $null }
    $report.Stages += $stage
    try { & $Action; $stage.Status = 'Passed' }
    catch { $stage.Error = $_.ToString(); throw }
    finally { $report | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $reportPath -Encoding utf8 }
}
function Invoke-UpgradeProcess([string] $Executable, [string[]] $Arguments) {
    $info = [Diagnostics.ProcessStartInfo]::new($Executable)
    $info.UseShellExecute = $false
    $info.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $info.Environment['VIRTUALPAPER_TEST_DATA_ROOT'] = $profile
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    try {
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) { $process.Kill($true); $process.WaitForExit(); throw 'Upgrade process timed out.' }
        if ($process.ExitCode -ne 0) { throw "Upgrade process failed: $Executable (exit $($process.ExitCode))" }
    } finally { $process.Dispose() }
}
function Get-UpgradeProbe([string] $Root) {
    $path = Join-Path $Root 'test/VirtualPaper.InstallerTest/bin/Release/net8.0-windows10.0.19041.0/VirtualPaper.InstallerTest.dll'
    if (-not (Test-Path -LiteralPath $path)) { throw "Build the complete matching Release solution first: $path" }
    return $path
}
function Invoke-UpgradeWizard([string] $Executable, [string] $Language, [string] $Stage, [switch] $Uninstall) {
    $action = if ($Uninstall) { '--wizard-uninstall' } else { '--wizard-install' }
    Invoke-UpgradeProcess (Get-Command dotnet).Source @((Get-UpgradeProbe $TestRoot), $action, $Executable,
        '--install-dir', $installDir, '--language', $Language, '--timeout-seconds', "$($TimeoutSeconds - 10)",
        '--results-file', (Join-Path $ResultsRoot "$Stage-wizard.json"))
}
function Assert-UpgradeInstalledFiles([string] $Root, $Artifact) {
    foreach ($file in @('VirtualPaper.exe', 'VirtualPaper.dll', 'VirtualPaper.Common.dll', 'Plugins/UI/VirtualPaper.UI.dll')) {
        $expected = Join-Path $Root "src/VirtualPaper/bin/Release/net8.0-windows10.0.19041.0/$file"
        if ((Get-FileHash -LiteralPath (Join-Path $installDir $file)).Hash -cne (Get-FileHash -LiteralPath $expected).Hash) {
            throw "Installed payload differs from its phase's Release build: $file"
        }
    }
    if ([version][Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $installDir 'VirtualPaper.exe')).FileVersion -ne [version]$Artifact.PackageVersion) {
        throw 'Installed product version does not match its artifact.'
    }
    $registrations = @(Get-InstallRegistrations (Get-InstallerAppId (Join-Path $TestRoot 'packaging/inno/setup.iss')))
    if (-not $registrations.Count -or @($registrations | Where-Object {
        -not $_.Location -or [IO.Path]::GetFullPath($_.Location).TrimEnd('\') -ine $installDir -or [version]$_.Version -ne [version]$Artifact.PackageVersion
    }).Count) { throw 'Installed registration does not match this upgrade version/location.' }
}
function Invoke-UpgradeProfile([string] $Name, [string] $Root, [switch] $Prepare) {
    if ($Channel -eq 'EXE') {
        $probe = Get-UpgradeProbe $Root
        if ((Get-FileHash -LiteralPath (Join-Path (Split-Path -Parent $probe) 'VirtualPaper.Common.dll')).Hash -cne
            (Get-FileHash -LiteralPath (Join-Path $installDir 'VirtualPaper.Common.dll')).Hash) { throw 'Probe and installed Core binaries differ.' }
        $mode = if ($Prepare) { 'prepare' } else { 'preserve' }
        $path = Join-Path $ResultsRoot "$Name-application.json"
        Invoke-UpgradeProcess (Get-Command dotnet).Source @($probe, '--install-dir', $installDir, '--data-root', $profile,
            '--profile-mode', $mode, '--timeout-seconds', "$($TimeoutSeconds - 20)", '--results-file', $path)
        $application = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        if ($application.Passed -cne $true -or $application.OpenedNormally -cne $true -or $application.ExitedNormally -cne $true -or
            $application.ForcedCleanup -cne $false -or $application.PersistenceVerified -cne $true -or $application.LanguageVerified -cne $true -or
            $application.ExpectedLanguage -cne 'en-US' -or $application.ActualLanguage -cne 'en-US' -or $application.PersistedLanguage -cne 'en-US') {
            throw 'Upgrade application/profile acceptance failed.'
        }
    } else {
        $module = if ($Prepare) { 'MsixProfile.Prepare' } else { 'MsixProfile.Verify' }
        $path = Join-Path $ResultsRoot "$Name/ui-operation-results.json"
        & "$PSScriptRoot/Run-UIOperationTests.ps1" -Configuration Release -NoBuild -PackageFullName $installed.PackageFullName `
            -HostExe (Join-Path $Root 'test/VirtualPaper.UIOperationTest/bin/x64/Release/net8.0-windows10.0.19041.0/VirtualPaper.UIOperationTest.exe') `
            -DataRoot $profile -Module $module -ReportPath $path *> (Join-Path $ResultsRoot "$Name.log")
        $null = Assert-MsixUIReport $path $installed.PackageFullName
    }
    Assert-NoMsixTestProcesses $installDir
}
function Assert-UpgradeMsixVersion($Artifact) {
    $actual = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $installDir 'VirtualPaper.exe')).FileVersion
    if ([version]$actual -ne [version]$Artifact.ProductVersion) { throw 'Packaged product version differs from the phase artifact.' }
}

try {
    if ($Channel -eq 'MSIX') {
        if (Get-AppxPackage -Name $previous.IdentityName) { throw 'The upgrade test identity is already installed.' }
        $public = Join-Path (Split-Path -Parent $PreviousArtifact) $previous.CertificateFile
        $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($public)
        if (Test-Path -LiteralPath "Cert:/LocalMachine/TrustedPeople/$($certificate.Thumbprint)") { throw 'The acceptance certificate is already trusted.' }
        Import-Certificate -FilePath $public -CertStoreLocation Cert:/LocalMachine/TrustedPeople | Out-Null
        $trusted = $true
        $report.Framework = Initialize-MsixTestFramework $ResultsRoot
    }
    Invoke-UpgradeStage InstallPrevious {
        $path = Resolve-PackageArtifactFile (Split-Path -Parent $PreviousArtifact) $previous.PackageFile $previous.PackageSha256
        $script:installationAttempted = $true
        if ($Channel -eq 'EXE') {
            Invoke-UpgradeWizard $path 'english' 'previous-install'
            Assert-UpgradeInstalledFiles $PreviousTestRoot $previous
        } else {
            Add-AppxPackage -Path $path -ErrorAction Stop
            $script:installed = Get-AppxPackage -Name $previous.IdentityName
            if (-not $installed -or $installed.Publisher -cne $previous.Publisher -or $installed.Version -ne [version]$previous.PackageVersion) { throw 'Previous MSIX identity/version differs.' }
            $script:installDir = $installed.InstallLocation
            $report.PreviousFullName = $installed.PackageFullName
            $report.PackageFamilyName = $installed.PackageFamilyName
            Assert-UpgradeMsixVersion $previous
        }
        $source = if ($Channel -eq 'MSIX') { Join-Path $installDir 'BuiltInWallpapers' } else {
            Join-Path $PreviousTestRoot 'src/VirtualPaper/bin/Release/net8.0-windows10.0.19041.0/BuiltInWallpapers'
        }
        $script:builtIns = New-BuiltInWallpaperAcceptance $source $profile
    }
    Invoke-UpgradeStage PreparePrevious {
        Invoke-UpgradeProfile 'previous-profile' $PreviousTestRoot -Prepare
        Assert-InitialBuiltInWallpapers $builtIns
        Initialize-BuiltInWallpaperReinstallFixture $builtIns
        $newPayload = Join-Path $TestRoot 'src/VirtualPaper/bin/Release/net8.0-windows10.0.19041.0/BuiltInWallpapers'
        Update-BuiltInWallpaperUpgradeExpectation $builtIns $newPayload
        $report.PreviousSettings = Get-Content -LiteralPath (Join-Path $profile 'user_settings.json') -Raw | ConvertFrom-Json
    }
    Invoke-UpgradeStage Upgrade {
        $path = Resolve-PackageArtifactFile (Split-Path -Parent $PackageArtifact) $next.PackageFile $next.PackageSha256
        Assert-NoMsixTestProcesses $installDir
        if ($Channel -eq 'EXE') {
            $registrations = @(Get-InstallRegistrations (Get-InstallerAppId (Join-Path $TestRoot 'packaging/inno/setup.iss')))
            if (-not $registrations.Count -or @($registrations | Where-Object { [IO.Path]::GetFullPath($_.Location).TrimEnd('\') -ine $installDir }).Count) {
                throw 'Upgrade must target only the installation created by this run.'
            }
            Invoke-UpgradeWizard $path 'chinese' 'upgrade-install'
            Assert-UpgradeInstalledFiles $TestRoot $next
            if ((Get-InstallerApplicationLanguage ([IO.File]::ReadAllText((Join-Path $installDir 'installer-language.txt')).Trim())) -cne 'zh-CN') {
                throw 'The upgrade did not write the newly selected installer language marker.'
            }
        } else {
            # Forward upgrade, without uninstall or ForceUpdateFromAnyVersion.
            Add-AppxPackage -Path $path -ErrorAction Stop
            $script:installed = Get-AppxPackage -Name $next.IdentityName
            if (-not $installed -or $installed.PackageFamilyName -cne $report.PackageFamilyName -or
                $installed.Publisher -cne $next.Publisher -or $installed.Version -ne [version]$next.PackageVersion -or
                $installed.PackageFullName -ceq $report.PreviousFullName) { throw 'MSIX did not perform a same-family forward upgrade.' }
            $script:installDir = $installed.InstallLocation
            $report.NextFullName = $installed.PackageFullName
            Assert-UpgradeMsixVersion $next
        }
    }
    Invoke-UpgradeStage VerifyRetained {
        Invoke-UpgradeProfile 'retained-profile' $TestRoot
        Assert-ReinstalledBuiltInWallpapers $builtIns
        $saved = Get-Content -LiteralPath (Join-Path $profile 'user_settings.json') -Raw | ConvertFrom-Json
        foreach ($key in @('Language', 'ApplicationTheme', 'NavigationAnimationsEnabled', 'WallpaperDir')) {
            if ($saved.$key -cne $report.PreviousSettings.$key) { throw "Upgrade replaced the saved setting: $key" }
        }
        if (-not $report.PreviousSettings.PSObject.Properties['UiHardwareAccelerationEnabled'] -and $saved.UiHardwareAccelerationEnabled -cne $true) {
            throw 'Upgrade did not initialize the new hardware-acceleration setting with its default.'
        }
        $report.NewSettingsInitialized = $true
    }
    Invoke-UpgradeStage Restart { Invoke-UpgradeProfile 'restart-profile' $TestRoot }
} catch { $report.Error = $_.ToString() }
finally {
    try {
        Invoke-UpgradeStage Uninstall {
            if ($installationAttempted) {
                $forced = Stop-OwnedMsixTestProcesses $installDir
                if ($Channel -eq 'MSIX') {
                    $owned = Get-AppxPackage -Name $previous.IdentityName
                    if ($owned) {
                        if ($owned.Publisher -cne $previous.Publisher) { throw 'Cleanup refused an unowned package.' }
                        Remove-AppxPackage -Package $owned.PackageFullName -ErrorAction Stop
                    }
                    if (Get-AppxPackage -Name $previous.IdentityName) { throw 'The test MSIX is still registered.' }
                } elseif (Test-Path -LiteralPath $installDir) {
                    $uninstallers = @(Get-ChildItem -LiteralPath $installDir -File -Filter 'unins*.exe')
                    if ($uninstallers.Count -ne 1) { throw 'Expected exactly one owned uninstaller.' }
                    Invoke-UpgradeWizard $uninstallers[0].FullName 'english' 'uninstall' -Uninstall
                    Assert-CleanInstallerEnvironment (Join-Path $TestRoot 'packaging/inno/setup.iss')
                    $timer = [Diagnostics.Stopwatch]::StartNew()
                    while (Test-Path -LiteralPath $installDir) {
                        if (-not @(Get-ChildItem -LiteralPath $installDir -File -Recurse).Count) { break }
                        if ($timer.Elapsed.TotalSeconds -ge 10) { throw 'Installed files remain after uninstall.' }
                        Start-Sleep -Milliseconds 100
                    }
                }
                if ($builtIns) { Assert-UninstalledBuiltInWallpapers $builtIns }
                if ($forced) { throw 'Forced process recovery was required; normal exit failed.' }
            }
        }
    } catch { $report.Error += "`nUninstall: $_" }
    if ($Channel -eq 'MSIX') {
        try {
            Invoke-UpgradeStage CertificateCleanup {
                if ($trusted) { Remove-Item -LiteralPath "Cert:/LocalMachine/TrustedPeople/$($certificate.Thumbprint)" }
            }
        } catch { $report.Error += "`nCertificate cleanup: $_" }
    }
    if ($builtIns) { $report.BuiltInWallpapers = $builtIns.Evidence }
    try {
        if (Test-Path -LiteralPath $profile) { Copy-Item -LiteralPath $profile -Destination (Join-Path $ResultsRoot 'profile') -Recurse }
        $expected = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) $profileFolder))
        if ([IO.Path]::GetFullPath($profiles) -cne $expected) { throw 'Unexpected upgrade profile cleanup path.' }
        # The installer owns application deletion. Retain failed install files for diagnosis.
        if (Test-Path -LiteralPath $profile) { Remove-Item -LiteralPath $profile -Recurse -Force }
        if ((Test-Path -LiteralPath $profiles) -and -not (Get-ChildItem -LiteralPath $profiles -Force)) { Remove-Item -LiteralPath $profiles }
    } catch { $report.Error += "`nProfile artifacts/cleanup: $_" }
    $report.Passed = -not $report.Error -and (@($report.Stages.Name) -join ',') -ceq (@(Get-UpgradeStages $Channel) -join ',') -and
        @($report.Stages | Where-Object Status -CNE 'Passed').Count -eq 0
    $report | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $reportPath -Encoding utf8
}
Assert-UpgradeReport ([pscustomobject]$report)
Write-Host "[OK] $Channel cross-version upgrade passed: $reportPath"
