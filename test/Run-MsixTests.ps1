#Requires -Version 7.2
[CmdletBinding()]
param(
    [string] $ResultsRoot = "TestResults/msix-acceptance-$(Get-Date -Format 'yyyyMMdd-HHmmss')-$([guid]::NewGuid().ToString('N').Substring(0, 8))",
    [switch] $IncludeMLIntegration,
    [string] $PackageArtifact,
    [ValidateSet('Full', 'Smoke', 'UI')][string] $Mode = 'Full'
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'MSIX acceptance requires Windows.' }
. "$PSScriptRoot/TestReportSupport.ps1"
. "$PSScriptRoot/UIOperationTestSupport.ps1"
. "$PSScriptRoot/MsixTestSupport.ps1"
. "$PSScriptRoot/BuiltInWallpaperTestSupport.ps1"
. "$PSScriptRoot/PackageArtifactSupport.ps1"
if (($Mode -ne 'Full') -ne [bool]$PackageArtifact) { throw 'Smoke/UI modes require an already built package artifact; Full builds its own package.' }
$artifact = if ($PackageArtifact) { Get-PackageArtifact -ManifestPath $PackageArtifact -Channel MSIX } else { $null }
if ($artifact -and $artifact.IdentityName -notmatch '^PaperHammer\.VirtualPaper\.Test\.[0-9a-f]{16}$' -and
    ($env:GITHUB_ACTIONS -cne 'true' -or $env:RUNNER_ENVIRONMENT -cne 'github-hosted')) {
    throw 'Formal package acceptance requires disposable GitHub-hosted CI; daily installations cannot be used.'
}
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$ResultsRoot = [IO.Path]::GetFullPath($ResultsRoot, $repositoryRoot)
if (Test-Path -LiteralPath $ResultsRoot) { throw 'MSIX acceptance results must use a new directory.' }
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run PowerShell as administrator to temporarily trust and remove the acceptance signing certificate.'
}
$msbuild = & "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild/**/Bin/MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'Visual Studio MSBuild is required.' }
Get-Command dotnet.exe,node.exe,Get-AppxPackage,Add-AppxPackage,Remove-AppxPackage,New-SelfSignedCertificate -ErrorAction Stop | Out-Null
[void](New-Item -ItemType Directory -Path $ResultsRoot)
$reportPath = Join-Path $ResultsRoot 'msix-acceptance.json'
$report = [ordered]@{ Passed = $false; Mode = $Mode; SourceSha = $artifact.SourceSha; Framework = $null
    PackageFullName = $null; PackageSha256 = $null; BuiltInWallpapers = $null; Stages = @(); Error = $null }
$runId = [guid]::NewGuid().ToString('N')
# An identity owned only by this run prevents overwriting a daily preview/Store installation.
$identity = "PaperHammer.VirtualPaper.Test.$($runId.Substring(0, 16))"
$publisher = "CN=VirtualPaper MSIX Acceptance $runId"
if ($artifact) { $identity = $artifact.IdentityName; $publisher = $artifact.Publisher }
$profiles = Join-Path ([IO.Path]::GetTempPath()) "VirtualPaper.MsixAcceptance-$runId"
$retained = Join-Path $profiles 'retained profile'
$certificate = $null
$installed = $null
$installationAttempted = $false
$builtIns = $null
$jobs = @()
$trustedCertificateCreated = $false
$ownershipPath = Join-Path $profiles 'ci-ownership.json'

function Invoke-Stage([string] $Name, [scriptblock] $Action) {
    $stage = [ordered]@{ Name = $Name; Status = 'Failed'; Error = $null }
    $report.Stages += $stage
    try { & $Action; $stage.Status = 'Passed' }
    catch { $stage.Error = $_.ToString(); throw }
    finally { $report | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $reportPath -Encoding utf8 }
}
function Invoke-PackageUI([string] $Name, [string] $Module, [string] $Profile, [switch] $Extended) {
    $path = Join-Path $ResultsRoot "$Name/ui-operation-results.json"
    $arguments = @{ Configuration = 'Release'; NoBuild = $true; PackageFullName = $installed.PackageFullName
        ReportPath = $path; HostExe = Join-Path $repositoryRoot 'test/VirtualPaper.UIOperationTest/bin/x64/Release/net8.0-windows10.0.19041.0/VirtualPaper.UIOperationTest.exe' }
    if ($artifact -and $identity -notmatch '^PaperHammer\.VirtualPaper\.Test\.[0-9a-f]{16}$') {
        $arguments.CIOwnershipPath = $ownershipPath
    }
    if ($Extended) { $arguments.Suite = 'Extended' } else { $arguments.Module = $Module; $arguments.DataRoot = $Profile }
    & "$PSScriptRoot/Run-UIOperationTests.ps1" @arguments *> (Join-Path $ResultsRoot "$Name.log")
    $null = Assert-MsixUIReport $path $installed.PackageFullName -Extended:$Extended
    Assert-NoMsixTestProcesses $installed.InstallLocation
}

Push-Location -LiteralPath $repositoryRoot
try {
    if ($Mode -eq 'Full') {
        Invoke-Stage Contracts { & ./.github/scripts/validate-test-workflow.ps1 *> "$ResultsRoot/contracts.log" }
        Invoke-Stage Build {
            & $msbuild src/VirtualPaper.sln /t:Rebuild /restore /p:Configuration=Release '/p:Platform=Any CPU' /m /nologo /v:minimal *> "$ResultsRoot/build.log"
            if ($LASTEXITCODE -ne 0) { throw "Release build failed. See $ResultsRoot/build.log" }
        }
        Invoke-Stage ProjectTests {
            foreach ($spec in Get-ProjectTestSpecs) {
                $script:jobs += Start-Job -Name $spec.Id -ArgumentList $repositoryRoot,$ResultsRoot,$spec.Id,$spec.Artifact,$IncludeMLIntegration.IsPresent -ScriptBlock {
                    param($root, $results, $jobId, $artifact, $includeML)
                    $ErrorActionPreference = 'Stop'
                    Set-Location -LiteralPath $root
                    if ($jobId -eq 'test-ui-logic') {
                        & node .github/scripts/test-atmosphere.cjs
                        if ($LASTEXITCODE -ne 0) { throw 'Atmosphere script tests failed.' }
                        & node .github/scripts/test-depth-player.cjs
                        if ($LASTEXITCODE -ne 0) { throw 'Depth player script tests failed.' }
                    }
                    & ./test/Run-ProjectTests.ps1 -JobId $jobId -ResultsDirectory (Join-Path $results $artifact) -IncludeMLIntegration:$includeML -RequireCompleteExecution
                }
            }
            $jobs | Wait-Job | Out-Null
            foreach ($job in $jobs) { Receive-Job $job -ErrorAction Continue *> (Join-Path $ResultsRoot "$($job.Name).log") }
            if (@($jobs | Where-Object State -NE Completed).Count) { throw 'Project tests failed; package installation was not attempted.' }
            Get-ProjectTestReports -Root $ResultsRoot -RequireCompleteExecution | Format-Table Total, Executed, Passed
        }
        Invoke-Stage Package {
            $script:certificate = New-SelfSignedCertificate -Type Custom -Subject $publisher -KeyUsage DigitalSignature `
                -CertStoreLocation Cert:/CurrentUser/My -NotAfter (Get-Date).AddDays(2) `
                -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}') -KeyExportPolicy NonExportable
            $publicCertificate = Join-Path $ResultsRoot 'acceptance.cer'
            Export-Certificate -Cert $certificate -FilePath $publicCertificate | Out-Null
            Import-Certificate -FilePath $publicCertificate -CertStoreLocation Cert:/LocalMachine/TrustedPeople | Out-Null
            $script:trustedCertificateCreated = $true
            & ./.github/scripts/Build-Msix.ps1 -SkipSolutionBuild -IdentityName $identity -Publisher $publisher `
                -CertificateThumbprint $certificate.Thumbprint -OutputDirectory "$ResultsRoot/package" *> "$ResultsRoot/package.log"
            $packages = @(Get-ChildItem -LiteralPath "$ResultsRoot/package" -Recurse -Filter '*.msix' -File |
                Where-Object FullName -NotMatch '[\\/]Dependencies[\\/]')
            if ($packages.Count -ne 1) { throw 'Expected one freshly built MSIX.' }
            $script:packagePath = $packages[0].FullName
            $report.PackageSha256 = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash
        }
    } else {
        $artifactRoot = Split-Path -Parent ([IO.Path]::GetFullPath($PackageArtifact))
        $packagePath = Join-Path $artifactRoot $artifact.PackageFile
        $report.PackageSha256 = $artifact.PackageSha256
        $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new((Join-Path $artifactRoot $artifact.CertificateFile))
        $trustPath = "Cert:/LocalMachine/TrustedPeople/$($certificate.Thumbprint)"
        if (Test-Path -LiteralPath $trustPath) { throw 'Acceptance certificate is already trusted; refusing to change existing trust.' }
        Import-Certificate -FilePath (Join-Path $artifactRoot $artifact.CertificateFile) -CertStoreLocation Cert:/LocalMachine/TrustedPeople | Out-Null
        $trustedCertificateCreated = $true
        & "$PSScriptRoot/Test-MsixPackage.ps1" -PackagePath $packagePath
    }
    $environment = Get-UIOperationEnvironment
    $environment | ConvertTo-Json -Depth 6 | Set-Content "$ResultsRoot/environment.json" -Encoding utf8
    if (-not $environment.Available) {
        $skippedStage = if ($Mode -eq 'UI') { 'ExtendedUI' } else { 'FirstRun' }
        $report.Stages += [ordered]@{ Name = $skippedStage; Status = 'Skipped'; Error = $environment.Reason }
        New-UIOperationSkipReport $environment | ConvertTo-Json -Depth 8 | Set-Content "$ResultsRoot/ui-skipped.json" -Encoding utf8
        throw "Required real MSIX UI acceptance was skipped: $($environment.Reason)"
    }
    Invoke-Stage Install {
        if (@(Get-AppxPackage -Name $identity).Count) { throw 'The per-run test identity already exists.' }
        $report.Framework = Initialize-MsixTestFramework $ResultsRoot
        $script:installationAttempted = $true
        Add-AppxPackage -Path $packagePath -ErrorAction Stop
        $script:installed = Get-AppxPackage -Name $identity
        if (-not $installed -or $installed.Publisher -cne $publisher) { throw 'The installed test identity does not match.' }
        $report.PackageFullName = $installed.PackageFullName
        if ($artifact) {
            [void](New-Item -ItemType Directory -Path $profiles -Force)
            @{ CreatedRegistration = $true; PackageFullName = $installed.PackageFullName
                PackageSha256 = $artifact.PackageSha256; ArtifactManifest = [IO.Path]::GetFullPath($PackageArtifact) } |
                ConvertTo-Json | Set-Content -LiteralPath $ownershipPath -Encoding utf8NoBOM
        }
    }
    if ($Mode -ne 'UI') {
        Invoke-Stage FirstRun {
            Invoke-PackageUI 'first-run' 'MsixProfile.Prepare' $retained
            $script:builtIns = New-BuiltInWallpaperAcceptance (Join-Path $installed.InstallLocation 'BuiltInWallpapers') $retained
            $report.BuiltInWallpapers = $builtIns.Evidence
            Assert-InitialBuiltInWallpapers $builtIns
            Initialize-BuiltInWallpaperReinstallFixture $builtIns
        }
    }
    if ($Mode -ne 'Smoke') { Invoke-Stage ExtendedUI { Invoke-PackageUI 'extended-ui' '' '' -Extended } }
    if ($Mode -ne 'UI') {
        Invoke-Stage Reinstall {
            Assert-NoMsixTestProcesses $installed.InstallLocation
            if ((Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash -ine $report.PackageSha256) { throw 'The package changed between install/reinstall.' }
            Add-AppxPackage -Path $packagePath -ForceUpdateFromAnyVersion -ErrorAction Stop
            if ((Get-AppxPackage -Name $identity).PackageFullName -cne $report.PackageFullName) { throw 'Reinstall changed the package identity.' }
        }
        Invoke-Stage RetainedProfile {
            Invoke-PackageUI 'retained-profile' 'MsixProfile.Verify' $retained
            Assert-ReinstalledBuiltInWallpapers $builtIns
        }
        Invoke-Stage FreshProfile { Invoke-PackageUI 'fresh-profile' 'MsixProfile.Prepare' (Join-Path $profiles 'fresh profile') }
    }
} catch {
    $report.Error = $_.ToString()
} finally {
    try {
        Invoke-Stage Uninstall {
            # Never remove packages by a broad name/publisher filter.
            $owned = if ($installationAttempted) { Get-AppxPackage -Name $identity } else { $null }
            if ($owned) {
                if ($owned.Publisher -cne $publisher) { throw 'Cleanup refused a package not signed by this run.' }
                $forced = Stop-OwnedMsixTestProcesses $owned.InstallLocation
                Remove-AppxPackage -Package $owned.PackageFullName -ErrorAction Stop
                if (Get-AppxPackage -Name $identity) { throw 'The test package is still registered.' }
                if ($builtIns) { Assert-UninstalledBuiltInWallpapers $builtIns }
                if ($forced) { throw 'Forced cleanup was required for test package processes; normal-exit acceptance failed.' }
            } elseif ($installed) { throw 'Test package registration disappeared before uninstall validation.' }
        }
    } catch { $report.Error += "`nCleanup: $_" }
    try {
        Invoke-Stage CertificateCleanup {
            if ($certificate) {
                $stores = @()
                if ($trustedCertificateCreated) { $stores += 'LocalMachine/TrustedPeople' }
                if ($Mode -eq 'Full') { $stores += 'CurrentUser/My' }
                foreach ($store in $stores) {
                    $certificatePath = "Cert:/$store/$($certificate.Thumbprint)"
                    if (Test-Path -LiteralPath $certificatePath) {
                        if ($store -eq 'CurrentUser/My') { Remove-Item -LiteralPath $certificatePath -DeleteKey }
                        else { Remove-Item -LiteralPath $certificatePath }
                    }
                }
            }
        }
    } catch { $report.Error += "`nCertificate cleanup: $_" }
    $jobs | Where-Object State -EQ Running | Stop-Job
    $jobs | Remove-Job -Force
    try {
        # Keep isolated profiles beside the report for diagnosis; never delete daily data.
        if (Test-Path -LiteralPath $profiles) {
            Copy-Item -LiteralPath $profiles -Destination (Join-Path $ResultsRoot 'profiles') -Recurse
            $expected = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) "VirtualPaper.MsixAcceptance-$runId"))
            if ([IO.Path]::GetFullPath($profiles) -cne $expected) { throw 'Unexpected MSIX profile cleanup path.' }
            Remove-Item -LiteralPath $profiles -Recurse -Force
        }
    } catch { $report.Error += "`nProfile artifacts/cleanup: $_" }
    $report.Passed = -not $report.Error -and @($report.Stages | Where-Object Status -NE Passed).Count -eq 0 -and $report.Stages.Count -eq @(Get-MsixAcceptanceStages $Mode).Count
    $report | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $reportPath -Encoding utf8
    Pop-Location
    Write-Host "MSIX acceptance reports: $ResultsRoot"
}
Assert-MsixAcceptanceReport ([pscustomobject]$report) $Mode
try { if ($Mode -eq 'Full') { & "$PSScriptRoot/Summarize-MsixTestResults.ps1" -Root $ResultsRoot } }
catch {
    $report.Passed = $false
    $report.Error = $_.ToString()
    $report | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $reportPath -Encoding utf8
    throw
}
