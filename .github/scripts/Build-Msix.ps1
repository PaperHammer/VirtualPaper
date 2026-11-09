#Requires -Version 7.2
[CmdletBinding()]
param(
    [string] $IdentityName = 'PaperHammer.VirtualPaper.Preview',
    [string] $Publisher = 'CN=PaperHammer',
    [string] $PublisherDisplayName = 'PaperHammer',
    [string] $PackageVersion = '1.0.0.0',
    [ValidatePattern('^[0-9a-fA-F]{40}$')][string] $CertificateThumbprint,
    [switch] $Store,
    [switch] $SkipSolutionBuild,
    [string] $OutputDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'MsixResources.ps1')
if (-not $IsWindows) { throw 'MSIX packaging requires Windows and Visual Studio MSIX Packaging Tools.' }
if ($PackageVersion -notmatch '^[1-9][0-9]*\.[0-9]+\.[0-9]+\.0$' -or
    @($PackageVersion.Split('.') | Where-Object { [long]$_ -gt 65535 }).Count) {
    throw 'PackageVersion must have four parts, a nonzero major, revision 0, and components <= 65535.'
}
if ($Store -and ($IdentityName -eq 'PaperHammer.VirtualPaper.Preview' -or
    -not $PSBoundParameters.ContainsKey('Publisher') -or -not $PSBoundParameters.ContainsKey('PublisherDisplayName'))) {
    throw 'Store builds require IdentityName, Publisher and PublisherDisplayName from Partner Center.'
}
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if (-not (Test-Path -LiteralPath $vswherePath)) { throw 'Visual Studio Installer/vswhere.exe was not found.' }
$msbuild = & $vswherePath -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' |
    Select-Object -First 1
if (-not $msbuild) { throw 'Visual Studio MSBuild was not found.' }
$runName = "msix-$PackageVersion-$([guid]::NewGuid().ToString('N'))"
$workDirectory = Join-Path $repositoryRoot "Msix/obj/$runName"
$payload = Join-Path $workDirectory 'Payload'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repositoryRoot "Msix/AppPackages/$runName" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'OutputDirectory must be new; existing packages are never overwritten.' }
[void](New-Item -ItemType Directory -Path $payload, $OutputDirectory)

function Invoke-MsBuild([string[]] $BuildArguments) {
    & $msbuild @BuildArguments /nologo /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { throw "MSBuild failed (exit $LASTEXITCODE): $($BuildArguments[0])" }
}

function Get-ExeOutputState([string[]] $Roots) {
    $state = @{}
    foreach ($root in $Roots) {
        foreach ($file in Get-ChildItem -LiteralPath $root -File -Recurse) {
            if ($file.Name -like '*.runtimeconfig.json') {
                $runtime = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
                if (($runtime.runtimeOptions.PSObject.Properties['frameworks'] -or $runtime.runtimeOptions.PSObject.Properties['framework']) -and
                    (Test-Path -LiteralPath (Join-Path $file.DirectoryName 'coreclr.dll'))) {
                    throw "Framework-dependent EXE output contains a stale self-contained runtime. Clean this generated directory and rebuild Release: $($file.DirectoryName)"
                }
            }
            $state[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        }
    }
    return ,$state
}

Push-Location -LiteralPath $repositoryRoot
try {
    if (-not $SkipSolutionBuild) {
        # Preserve the solution's existing per-project platform mappings.
        Invoke-MsBuild @('src/VirtualPaper.sln', '/restore', '/t:Rebuild', '/p:Configuration=Release', '/p:Platform=Any CPU', '/m')
    }
    $exeOutputRoots = @('src/VirtualPaper/Plugins/UI', 'src/VirtualPaper/Plugins/PlayerWeb',
        'src/VirtualPaper/Plugins/ScrSaver', 'src/VirtualPaper/bin/Release/net8.0-windows10.0.19041.0') |
        ForEach-Object { Join-Path $repositoryRoot $_ }
    $exeOutputBefore = Get-ExeOutputState $exeOutputRoots

    # Publish each process with its own .NET runtime. These properties apply only
    # to this packaging invocation; normal EXE builds keep their existing settings.
    foreach ($process in @(
        @{ Project = 'VirtualPaper'; Platform = 'AnyCPU'; Folder = '' },
        @{ Project = 'VirtualPaper.UI'; Platform = 'AnyCPU'; Folder = 'Plugins/UI' },
        @{ Project = 'VirtualPaper.PlayerWeb'; Platform = 'AnyCPU'; Folder = 'Plugins/PlayerWeb' },
        @{ Project = 'VirtualPaper.ScreenSaver'; Platform = 'AnyCPU'; Folder = 'Plugins/ScrSaver' }
    )) {
        $publishDirectory = Join-Path $payload $process.Folder
        Invoke-MsBuild @("src/$($process.Project)/$($process.Project).csproj", '/restore', '/t:Publish',
            '/p:Configuration=Release', "/p:Platform=$($process.Platform)", '/p:RuntimeIdentifier=win-x64',
            '/p:PlatformTarget=x64', '/p:AppendRuntimeIdentifierToOutputPath=false',
            '/p:SelfContained=true', '/p:WindowsAppSDKSelfContained=true', '/p:WindowsPackageType=None',
            '/p:GenerateAppxPackageOnBuild=false', '/p:IsMsixRelease=false', '/p:BuildProjectReferences=false',
            "/p:CustomAfterMicrosoftCommonTargets=$repositoryRoot\Msix\Publish.props",
            "/p:MsixPublishProject=$($process.Project)", "/p:MsixPublishOutputRoot=$workDirectory\ProcessBuild",
            "/p:PublishDir=$publishDirectory\")
    }

    $exeOutputAfter = Get-ExeOutputState $exeOutputRoots
    $changed = @(@($exeOutputBefore.Keys) + @($exeOutputAfter.Keys) | Sort-Object -Unique | Where-Object {
        -not $exeOutputBefore.ContainsKey($_) -or -not $exeOutputAfter.ContainsKey($_) -or $exeOutputBefore[$_] -cne $exeOutputAfter[$_]
    })
    if ($changed.Count) { throw "MSIX publishing changed normal EXE output: $($changed -join ', ')" }
    Write-Output 'Normal EXE output fingerprints are unchanged after MSIX publishing.'

    $releaseDirectory = Join-Path $repositoryRoot 'src/VirtualPaper/bin/Release/net8.0-windows10.0.19041.0'
    foreach ($folder in @('ML', 'Shaders')) {
        Copy-Item -LiteralPath (Join-Path $releaseDirectory "Plugins/$folder") -Destination (Join-Path $payload 'Plugins') -Recurse
    }

    # Preserve WinUI URI names and embedded XBF; relocate only file candidates.
    $uiDirectory = Join-Path $payload 'Plugins/UI'
    $sdkRoot = (Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Microsoft SDKs\Windows\v10.0').InstallationFolder
    $makePri = Join-Path $sdkRoot 'bin/10.0.26100.0/x64/makepri.exe'
    if (-not (Test-Path -LiteralPath $makePri)) { throw 'Windows SDK 10.0.26100 MakePri was not found.' }
    Convert-MsixUIResources -UiDirectory $uiDirectory -PayloadDirectory $payload `
        -UiBuildDirectory (Join-Path $releaseDirectory 'Plugins/UI') -UiSourceDirectory (Join-Path $repositoryRoot 'src/VirtualPaper.UI') `
        -WorkDirectory (Join-Path $workDirectory 'Pri') -MakePri $makePri
    Get-ChildItem -LiteralPath $payload -Recurse -Filter '*.pdb' -File | Remove-Item

    # XML avoids command-line escaping of publisher DNs containing commas/semicolons.
    $identityProps = Join-Path $workDirectory 'Identity.props'
    $properties = [System.Xml.Linq.XElement]::new([System.Xml.Linq.XName]::Get('PropertyGroup'))
    foreach ($entry in @{
        MsixIdentityName = $IdentityName; MsixPublisher = $Publisher
        MsixPublisherDisplayName = $PublisherDisplayName; MsixPackageVersion = $PackageVersion
    }.GetEnumerator()) {
        $properties.Add([System.Xml.Linq.XElement]::new([System.Xml.Linq.XName]::Get($entry.Key), $entry.Value))
    }
    [System.Xml.Linq.XElement]::new([System.Xml.Linq.XName]::Get('Project'), $properties).Save($identityProps)
    $mode = if ($Store) { 'StoreUpload' } else { 'SideloadOnly' }
    $packageArguments = @('Msix/VirtualPaper.Package.wapproj', '/restore', '/t:Build', '/p:Configuration=Release', '/p:Platform=x64',
        "/p:MsixPayloadDirectory=$payload", "/p:MsixIdentityProps=$identityProps", "/p:AppxPackageDir=$OutputDirectory\",
        "/p:UapAppxPackageBuildMode=$mode")
    if ($CertificateThumbprint) {
        $packageArguments += @('/p:AppxPackageSigningEnabled=true', "/p:PackageCertificateThumbprint=$CertificateThumbprint")
    }
    Invoke-MsBuild $packageArguments

    $packages = @(Get-ChildItem -LiteralPath $OutputDirectory -Recurse -File |
        Where-Object { $_.Extension -in @('.msix', '.appx') -and $_.FullName -notmatch '[\\/]Dependencies[\\/]' })
    if ($packages.Count -ne 1) { throw "Expected one x64 application package, found $($packages.Count)." }
    & (Join-Path $repositoryRoot 'test/Test-MsixPackage.ps1') -PackagePath $packages[0].FullName
    Write-Output "Package: $($packages[0].FullName)"
    if (-not $CertificateThumbprint) {
        Write-Output 'Unsigned: Store re-signs submissions; side-loading requires a trusted test certificate matching Publisher.'
    }
} finally { Pop-Location }
