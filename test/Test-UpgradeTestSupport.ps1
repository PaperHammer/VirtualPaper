#Requires -Version 7.2
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/BuiltInWallpaperTestSupport.ps1"
. "$PSScriptRoot/UpgradeTestSupport.ps1"
function Require([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
function Reject([scriptblock] $Action) { try { & $Action | Out-Null } catch { return }; throw 'Invalid upgrade input was accepted.' }
$root = Join-Path ([IO.Path]::GetTempPath()) "VPUpgrade-contract-$([guid]::NewGuid().ToString('N'))"
function New-Artifact([string] $Name, [string] $Channel, [string] $Version, [string] $Source,
    [string] $Identity = 'PaperHammer.VirtualPaper.Test.0123456789abcdef', [string] $Publisher = 'CN=Upgrade Test', [string] $Certificate = 'public') {
    $folder = Join-Path $root $Name
    [void](New-Item -ItemType Directory -Path $folder -Force)
    $artifact = @{ FormatVersion = 1; Channel = $Channel; PackageVersion = $Version; SourceSha = $Source }
    if ($Channel -eq 'EXE') {
        $artifact.PackageFile = 'setup.exe'
        Set-Content -LiteralPath (Join-Path $folder $artifact.PackageFile) "$Source-$Version"
    } else {
        $artifact.PackageFile = 'application.msix'; $artifact.ProductVersion = '0.6.0.0'
        $artifact.IdentityName = $Identity; $artifact.Publisher = $Publisher; $artifact.Store = $false
        $artifact.CertificateFile = 'test.cer'
        Set-Content -LiteralPath (Join-Path $folder $artifact.CertificateFile) $Certificate
        $artifact.CertificateSha256 = (Get-FileHash -LiteralPath (Join-Path $folder $artifact.CertificateFile)).Hash
        $path = Join-Path $folder $artifact.PackageFile
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
        $archive = [IO.Compression.ZipFile]::Open($path, 'Create')
        try {
            foreach ($entry in @{
                'AppxManifest.xml' = "<Package><Identity Name=`"$Identity`" Publisher=`"$Publisher`" Version=`"$Version`" ProcessorArchitecture=`"x64`"/></Package>"
                'AppxSignature.p7x' = "signature-$Source"
            }.GetEnumerator()) {
                $writer = [IO.StreamWriter]::new($archive.CreateEntry($entry.Key).Open())
                try { $writer.Write($entry.Value) } finally { $writer.Dispose() }
            }
        } finally { $archive.Dispose() }
    }
    $artifact.PackageSha256 = (Get-FileHash -LiteralPath (Join-Path $folder $artifact.PackageFile)).Hash
    $manifest = Join-Path $folder 'package.json'
    $artifact | ConvertTo-Json | Set-Content -LiteralPath $manifest -Encoding utf8
    return $manifest
}
try {
    # A framework upgrade must use the matching tools/payload from each snapshot,
    # even when stale output from the other framework is also present.
    foreach ($major in @(8, 10)) {
        $snapshot = Join-Path $root "net$major"
        foreach ($project in @('VirtualPaper', 'VirtualPaper.InstallerTest', 'VirtualPaper.UIOperationTest')) {
            $parent = if ($project -eq 'VirtualPaper') { 'src' } else { 'test' }
            $directory = Join-Path $snapshot "$parent/$project"
            [void](New-Item -ItemType Directory -Path $directory -Force)
            $framework = "net$major.0-windows10.0.19041.0"
            Set-Content -LiteralPath (Join-Path $directory "$project.csproj") "<Project><PropertyGroup><TargetFramework>$framework</TargetFramework></PropertyGroup></Project>"
            $platform = if ($project -eq 'VirtualPaper.UIOperationTest') { 'x64/' } else { '' }
            $expected = Join-Path $directory "bin/${platform}Release/$framework"
            [void](New-Item -ItemType Directory -Path $expected -Force)
            [void](New-Item -ItemType Directory -Path (Join-Path $directory "bin/${platform}Release/net99.0-windows10.0.19041.0") -Force)
            Require ((Get-UpgradeBuildDirectory $snapshot $project) -ceq $expected) 'Upgrade selected another snapshot/framework output.'
        }
    }
    Set-Content -LiteralPath (Join-Path $directory "$project.csproj") '<Project><PropertyGroup><TargetFramework>../other</TargetFramework></PropertyGroup></Project>'
    Reject { Get-UpgradeBuildDirectory $snapshot $project }
    foreach ($channel in @('EXE', 'MSIX')) {
        $oldVersion = if ($channel -eq 'EXE') { '0.6.0.0' } else { '1.0.0.0' }
        $newVersion = if ($channel -eq 'EXE') { '0.6.1.0' } else { '1.0.1.0' }
        $old = New-Artifact "$channel-old" $channel $oldVersion ('a' * 40)
        $next = New-Artifact "$channel-new" $channel $newVersion ('b' * 40)
        $pair = Get-UpgradePackagePair $old $next $channel
        Require ($pair.Next.PackageVersion -ceq $newVersion) 'A valid forward upgrade was rejected.'
        Reject { Get-UpgradePackagePair $old $old $channel }
        Reject { Get-UpgradePackagePair $next $old $channel }
        $sameSource = New-Artifact "$channel-same-source" $channel $newVersion ('a' * 40)
        Reject { Get-UpgradePackagePair $old $sameSource $channel }
        $sameVersion = New-Artifact "$channel-same-version" $channel $oldVersion ('b' * 40)
        Reject { Get-UpgradePackagePair $old $sameVersion $channel }
        $report = @{ Suite = 'CrossVersionUpgrade'; Channel = $channel; Passed = $true; Previous = $pair.Previous; Next = $pair.Next; Error = $null
            NewSettingsInitialized = $true
            BuiltInWallpapers = @{ ExistingDataPreserved = $true; MissingFolderRestored = $true; SavedDirectoryHonored = $true; UninstallDataPreserved = $true }
            Stages = @(Get-UpgradeStages $channel | ForEach-Object { @{ Name = $_; Status = 'Passed' } }) }
        Assert-UpgradeReport ([pscustomobject]$report)
        $otherChannel = if ($channel -eq 'EXE') { 'MSIX' } else { 'EXE' }
        foreach ($artifact in @($report.Previous, $report.Next)) {
            $artifact.Channel = $otherChannel
            Reject { Assert-UpgradeReport ([pscustomobject]$report) }
            $artifact.Channel = $channel
        }
        if ($channel -eq 'MSIX') {
            $relabeled = @{} + $report
            $relabeled.Channel = 'EXE'
            $relabeled.Stages = @($report.Stages | Where-Object Name -NE 'CertificateCleanup')
            Reject { Assert-UpgradeReport ([pscustomobject]$relabeled) }
        }
        $report.NewSettingsInitialized = $false
        Reject { Assert-UpgradeReport ([pscustomobject]$report) }
        $report.NewSettingsInitialized = $true
        foreach ($key in @($report.BuiltInWallpapers.Keys)) {
            $report.BuiltInWallpapers[$key] = $false
            Reject { Assert-UpgradeReport ([pscustomobject]$report) }
            $report.BuiltInWallpapers[$key] = $true
        }
        foreach ($stage in $report.Stages) {
            $stage.Status = 'Skipped'
            Reject { Assert-UpgradeReport ([pscustomobject]$report) }
            $stage.Status = 'Passed'
        }
        $report.Suite = 'InstallerSmoke'
        Reject { Assert-UpgradeReport ([pscustomobject]$report) }
        if ($channel -eq 'MSIX') {
            foreach ($invalid in @(
                (New-Artifact 'other-family' MSIX $newVersion ('b' * 40) -Identity 'PaperHammer.VirtualPaper.Test.fedcba9876543210'),
                (New-Artifact 'other-publisher' MSIX $newVersion ('b' * 40) -Publisher 'CN=Another Publisher'),
                (New-Artifact 'other-certificate' MSIX $newVersion ('b' * 40) -Certificate 'another certificate'),
                (New-Artifact 'formal-family' MSIX $newVersion ('b' * 40) -Identity 'PaperHammer.VirtualPaper.Preview')
            )) { Reject { Get-UpgradePackagePair $old $invalid MSIX } }
        }
        Add-Content -LiteralPath (Join-Path (Split-Path -Parent $next) $pair.Next.PackageFile) 'tampered'
        Reject { Get-UpgradePackagePair $old $next $channel }
    }
    $source = Join-Path $root 'new-payload'
    $destination = Join-Path $root 'library'
    foreach ($name in @('existing', 'missing', 'new')) {
        [void](New-Item -ItemType Directory -Path (Join-Path $source $name) -Force)
        Set-Content -LiteralPath (Join-Path $source "$name/media") 'new bytes'
    }
    [void](New-Item -ItemType Directory -Path (Join-Path $destination 'existing') -Force)
    Set-Content -LiteralPath (Join-Path $destination 'existing/media') 'user-edited bytes'
    Set-Content -LiteralPath (Join-Path $destination 'existing/note') 'user note'
    Set-Content -LiteralPath (Join-Path $source 'existing/deleted-by-user') 'do not restore'
    $state = @{ Destination = $destination }
    Update-BuiltInWallpaperUpgradeExpectation $state $source
    Require ($state.Expected.Count -eq 4) 'Upgrade should retain existing folders and append only whole missing folders.'
    Require ($state.Expected[(Join-Path 'existing' 'media')] -ceq (Get-FileHash (Join-Path $destination 'existing/media')).Hash) 'Upgrade overwrote user media in its expectation.'
    Require (-not $state.Expected.ContainsKey((Join-Path 'existing' 'deleted-by-user'))) 'Upgrade restored a deleted file inside an existing folder.'
    # Exercise the actual packaging script with a compiler double: stale shared outputs
    # must not be read or removed, and a compiler failure must restore setup.iss.
    $repository = Join-Path $root 'packaging-repository'
    foreach ($folder in @('.github/scripts', 'packaging/inno/Output', 'src/VirtualPaper/bin/Release/net10.0-windows10.0.19041.0')) {
        [void](New-Item -ItemType Directory -Path (Join-Path $repository $folder) -Force)
    }
    $packager = Join-Path $repository '.github/scripts/New-CIExePackage.ps1'
    Copy-Item -LiteralPath "$PSScriptRoot/../.github/scripts/New-CIExePackage.ps1" -Destination $packager
    Copy-Item -LiteralPath (Get-Process -Id $PID).Path -Destination (Join-Path $repository 'src/VirtualPaper/bin/Release/net10.0-windows10.0.19041.0/VirtualPaper.exe')
    $setup = Join-Path $repository 'packaging/inno/setup.iss'
    $original = '#define MyAppVersion "0.6.0.0"' + "`n" + '#define MyAppVersionText "0_6_0_0"'
    [IO.File]::WriteAllText($setup, $original)
    $stale = Join-Path $repository 'packaging/inno/Output/old.exe'
    Set-Content -LiteralPath $stale 'old output'
    $compiler = Join-Path $root 'compiler.ps1'
    @'
param($Setup, $Output)
if ($Output -notmatch '^/O(.+)$' -or $Output -ceq '/O+') { throw 'A dedicated output directory is required.' }
[IO.File]::WriteAllText((Join-Path $Matches[1] 'fresh.exe'), 'fresh payload')
$global:LASTEXITCODE = 0
'@ | Set-Content -LiteralPath $compiler
    $output = Join-Path $root 'packaged-exe'
    & $packager -SourceSha ('c' * 40) -OutputDirectory $output -InnoSetupCompiler $compiler
    $null = Get-PackageArtifact (Join-Path $output 'package.json') EXE
    Require ([IO.File]::ReadAllText($setup) -ceq $original) 'Successful packaging did not restore setup.iss.'
    Require ((Get-Content -LiteralPath $stale -Raw).Trim() -ceq 'old output') 'Packaging changed a previous output.'
    Reject { & $packager -SourceSha ('c' * 40) -OutputDirectory $output -InnoSetupCompiler $compiler }
    'throw "Compiler failed"' | Set-Content -LiteralPath $compiler
    Reject { & $packager -SourceSha ('c' * 40) -OutputDirectory (Join-Path $root 'failed-output') -InnoSetupCompiler $compiler }
    Require ([IO.File]::ReadAllText($setup) -ceq $original) 'Failed packaging did not restore setup.iss.'
    Write-Host '[OK] Cross-version inputs, strict reports, family/signing guards, append-only wallpapers and isolated EXE outputs passed.'
} finally {
    $expected = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetFileName($root))))
    if ([IO.Path]::GetFullPath($root) -cne $expected -or [IO.Path]::GetFileName($root) -notmatch '^VPUpgrade-contract-[0-9a-f]{32}$') { throw 'Unexpected upgrade contract cleanup path.' }
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
