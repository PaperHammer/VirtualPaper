#Requires -Version 7.2
# Build two already-compiled source snapshots with one owned MSIX family/certificate.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $PreviousRoot,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string] $PreviousSourceSha,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string] $SourceSha,
    [string] $RepositoryRoot = ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))),
    [Parameter(Mandatory)][string] $OutputDirectory,
    [string] $InnoSetupCompiler,
    [string] $PreviousMsixVersion = '1.0.0.0',
    [string] $MsixVersion = '1.0.1.0'
)
$ErrorActionPreference = 'Stop'
if ($PreviousSourceSha -ceq $SourceSha -or [version]$MsixVersion -le [version]$PreviousMsixVersion) { throw 'Use two source snapshots and increasing package versions.' }
$PreviousRoot = (Resolve-Path -LiteralPath $PreviousRoot).Path
$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Paired packages require a fresh output directory.' }
$oldVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $PreviousRoot 'src/VirtualPaper/bin/Release/net8.0-windows10.0.19041.0/VirtualPaper.exe')).FileVersion
$newVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $RepositoryRoot 'src/VirtualPaper/bin/Release/net8.0-windows10.0.19041.0/VirtualPaper.exe')).FileVersion
if ([version]$newVersion -le [version]$oldVersion) { throw 'Build the candidate with a higher product FileVersion first; do not modify a released installer.' }
[void](New-Item -ItemType Directory -Path $OutputDirectory)
$runId = [guid]::NewGuid().ToString('N')
$identity = "PaperHammer.VirtualPaper.Test.$($runId.Substring(0, 16))"
$publisher = "CN=VirtualPaper Upgrade Acceptance $runId"
$certificate = $null
try {
    $certificate = New-SelfSignedCertificate -Type Custom -Subject $publisher -KeyUsage DigitalSignature `
        -CertStoreLocation Cert:/CurrentUser/My -NotAfter (Get-Date).AddDays(2) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}') -KeyExportPolicy NonExportable
    foreach ($phase in @(
        @{ Name = 'previous'; Root = $PreviousRoot; Sha = $PreviousSourceSha; Product = $oldVersion; Version = $PreviousMsixVersion },
        @{ Name = 'next'; Root = $RepositoryRoot; Sha = $SourceSha; Product = $newVersion; Version = $MsixVersion }
    )) {
        $output = Join-Path $OutputDirectory $phase.Name
        [void](New-Item -ItemType Directory -Path $output)
        & (Join-Path $phase.Root '.github/scripts/New-CIExePackage.ps1') -SourceSha $phase.Sha -Version $phase.Product `
            -OutputDirectory (Join-Path $output 'exe') -InnoSetupCompiler $InnoSetupCompiler *> (Join-Path $output 'exe.log')
        if ($LASTEXITCODE -ne 0) { throw "EXE packaging failed: $($phase.Name)" }
        $msixRoot = Join-Path $output 'msix'
        [void](New-Item -ItemType Directory -Path $msixRoot)
        $public = Join-Path $msixRoot 'acceptance.cer'
        Export-Certificate -Cert $certificate -FilePath $public | Out-Null
        & (Join-Path $phase.Root '.github/scripts/Build-Msix.ps1') -SkipSolutionBuild -IdentityName $identity -Publisher $publisher `
            -PackageVersion $phase.Version -CertificateThumbprint $certificate.Thumbprint -OutputDirectory (Join-Path $msixRoot 'build') *> (Join-Path $output 'msix.log')
        $packages = @(Get-ChildItem -LiteralPath (Join-Path $msixRoot 'build') -Recurse -File -Filter '*.msix' |
            Where-Object FullName -NotMatch '[\\/]Dependencies[\\/]')
        if ($packages.Count -ne 1) { throw "Expected one $($phase.Name) MSIX." }
        $package = Copy-Item -LiteralPath $packages[0].FullName -Destination $msixRoot -PassThru
        @{ FormatVersion = 1; Channel = 'MSIX'; SourceSha = $phase.Sha; Store = $false; ProductVersion = $phase.Product
            IdentityName = $identity; Publisher = $publisher; PackageVersion = $phase.Version
            PackageFile = $package.Name; PackageSha256 = (Get-FileHash -LiteralPath $package.FullName).Hash
            CertificateFile = 'acceptance.cer'; CertificateSha256 = (Get-FileHash -LiteralPath $public).Hash } |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $msixRoot 'package.json') -Encoding utf8NoBOM
    }
} finally {
    if ($certificate) { Remove-Item -LiteralPath "Cert:/CurrentUser/My/$($certificate.Thumbprint)" -DeleteKey }
}
Write-Host "Paired EXE/MSIX upgrade artifacts: $OutputDirectory"
