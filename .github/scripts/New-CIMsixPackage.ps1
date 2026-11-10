#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string] $SourceSha,
    [string] $OutputDirectory = 'dist/msix',
    [string] $IdentityName,
    [string] $Publisher,
    [string] $PublisherDisplayName,
    [string] $PackageVersion = '1.0.0.0',
    [switch] $Store
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/../../test/PackageArtifactSupport.ps1"
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a fresh MSIX artifact directory.' }
if ($Store -and (-not $IdentityName -or -not $Publisher -or -not $PublisherDisplayName)) {
    throw 'Store packaging requires the formal Partner Center identity.'
}
$run = [guid]::NewGuid().ToString('N')
if (-not $Store) {
    $IdentityName = "PaperHammer.VirtualPaper.Test.$($run.Substring(0, 16))"
    $Publisher = "CN=VirtualPaper MSIX Acceptance $run"
    $PublisherDisplayName = 'VirtualPaper CI'
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
[void](New-Item -ItemType Directory -Path $OutputDirectory)
$certificate = $null
try {
    # Only a public .cer crosses jobs. The non-exportable private key is deleted here.
    $certificate = New-SelfSignedCertificate -Type Custom -Subject $Publisher -KeyUsage DigitalSignature `
        -CertStoreLocation Cert:/CurrentUser/My -NotAfter (Get-Date).AddYears(1) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}') -KeyExportPolicy NonExportable
    $public = Join-Path $OutputDirectory 'acceptance.cer'
    Export-Certificate -Cert $certificate -FilePath $public | Out-Null
    & "$PSScriptRoot/Build-Msix.ps1" -SkipSolutionBuild -Store:$Store -IdentityName $IdentityName `
        -Publisher $Publisher -PublisherDisplayName $PublisherDisplayName -PackageVersion $PackageVersion `
        -CertificateThumbprint $certificate.Thumbprint -OutputDirectory (Join-Path $OutputDirectory 'build')
    $packages = @(Get-ChildItem "$OutputDirectory/build" -Recurse -File -Filter '*.msix' |
        Where-Object FullName -NotMatch '[\\/]Dependencies[\\/]')
    if ($packages.Count -ne 1) { throw 'Expected one signed x64 MSIX.' }
    $package = Copy-Item -LiteralPath $packages[0].FullName -Destination $OutputDirectory -PassThru
    $productExe = Join-Path $PSScriptRoot '../../src/VirtualPaper/bin/Release/net8.0-windows10.0.19041.0/VirtualPaper.exe'
    $manifest = [ordered]@{ FormatVersion = 1; Channel = 'MSIX'; SourceSha = $SourceSha; Store = $Store.IsPresent
        ProductVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($productExe).FileVersion
        IdentityName = $IdentityName; Publisher = $Publisher; PackageVersion = $PackageVersion
        PackageFile = $package.Name; PackageSha256 = (Get-FileHash $package.FullName).Hash
        CertificateFile = 'acceptance.cer'; CertificateSha256 = (Get-FileHash $public).Hash }
    if ($Store) {
        $uploads = @(Get-ChildItem "$OutputDirectory/build" -Recurse -File |
            Where-Object Extension -In @('.msixupload', '.appxupload'))
        if ($uploads.Count -ne 1) { throw 'Expected one Store upload archive.' }
        $upload = Copy-Item -LiteralPath $uploads[0].FullName -Destination $OutputDirectory -PassThru
        $manifest.UploadFile = $upload.Name
        $manifest.UploadSha256 = (Get-FileHash $upload.FullName).Hash
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content "$OutputDirectory/package.json" -Encoding utf8NoBOM
    $null = Get-PackageArtifact "$OutputDirectory/package.json" MSIX $SourceSha
} finally {
    if ($certificate) { Remove-Item -LiteralPath "Cert:/CurrentUser/My/$($certificate.Thumbprint)" -DeleteKey }
}
