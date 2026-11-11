# Shared artifact contracts for packaging, installed tests and release drafts.
function Get-MsixPayloadHash($Archive) {
    $digest = [Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
    try {
        foreach ($entry in @($Archive.Entries | Sort-Object FullName)) {
            # WAP signs the upload/side-load packages separately. Only these two
            # signature artifacts may differ; every other entry is compared.
            if ($entry.FullName -in @('AppxSignature.p7x', 'AppxMetadata/CodeIntegrity.cat')) { continue }
            $digest.AppendData([Text.Encoding]::UTF8.GetBytes($entry.FullName + [char]0))
            $stream = $entry.Open()
            try { $digest.AppendData([Security.Cryptography.SHA256]::HashData($stream)) }
            finally { $stream.Dispose() }
        }
        return [Convert]::ToHexString($digest.GetHashAndReset())
    } finally { $digest.Dispose() }
}

function Resolve-PackageArtifactFile([string] $Root, [string] $Name, [string] $Sha256) {
    if (-not $Name -or $Name -match '[/\\:]|^\.{1,2}$' -or $Sha256 -notmatch '^[0-9a-fA-F]{64}$') {
        throw 'Invalid package artifact filename or SHA256.'
    }
    $path = Join-Path $Root $Name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ine $Sha256) {
        throw "Missing or changed package artifact: $Name"
    }
    return $path
}

function Get-PackageArtifact {
    param([Parameter(Mandatory)][string] $ManifestPath,
        [Parameter(Mandatory)][ValidateSet('EXE', 'MSIX')][string] $Channel,
        [string] $SourceSha)
    $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    if ($manifest.FormatVersion -ne 1 -or $manifest.Channel -cne $Channel -or
        $manifest.SourceSha -notmatch '^[0-9a-f]{40}$' -or ($SourceSha -and $manifest.SourceSha -cne $SourceSha)) {
        throw 'Package channel or source snapshot does not match this run.'
    }
    $root = Split-Path -Parent ([IO.Path]::GetFullPath($ManifestPath))
    $package = Resolve-PackageArtifactFile $root $manifest.PackageFile $manifest.PackageSha256
    if (($Channel -eq 'EXE' -and [IO.Path]::GetExtension($package) -ine '.exe') -or
        ($Channel -eq 'MSIX' -and [IO.Path]::GetExtension($package) -ine '.msix')) {
        throw 'Unexpected package extension.'
    }
    if ($Channel -eq 'MSIX') {
        if ($manifest.ProductVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'MSIX product version is missing.' }
        $null = Resolve-PackageArtifactFile $root $manifest.CertificateFile $manifest.CertificateSha256
        if ($manifest.PackageVersion -notmatch '^[1-9][0-9]*\.[0-9]+\.[0-9]+\.0$') { throw 'Invalid MSIX package version.' }
        $archive = [IO.Compression.ZipFile]::OpenRead($package)
        try {
            $entry = $archive.GetEntry('AppxManifest.xml')
            if (-not $entry -or -not $archive.GetEntry('AppxSignature.p7x')) { throw 'MSIX must contain a manifest and signature.' }
            $reader = [IO.StreamReader]::new($entry.Open())
            try { [xml]$xml = $reader.ReadToEnd() } finally { $reader.Dispose() }
            $identity = $xml.Package.Identity
            if ($identity.Name -cne $manifest.IdentityName -or $identity.Publisher -cne $manifest.Publisher -or
                $identity.Version -cne $manifest.PackageVersion -or $identity.ProcessorArchitecture -cne 'x64') {
                throw 'MSIX manifest differs from the package artifact metadata.'
            }
        } finally { $archive.Dispose() }
        if ($manifest.Store -eq $true) {
            $upload = Resolve-PackageArtifactFile $root $manifest.UploadFile $manifest.UploadSha256
            if ([IO.Path]::GetExtension($upload) -notin @('.msixupload', '.appxupload')) { throw 'Store upload archive is missing.' }
            $signed = [IO.Compression.ZipFile]::OpenRead($package)
            try { $payloadHash = Get-MsixPayloadHash $signed } finally { $signed.Dispose() }
            $archive = [IO.Compression.ZipFile]::OpenRead($upload)
            try {
                $entries = @($archive.Entries | Where-Object { [IO.Path]::GetExtension($_.FullName) -in @('.msix', '.appx') })
                if ($entries.Count -ne 1) { throw 'Store upload must contain exactly the tested x64 package.' }
                $stream = $entries[0].Open()
                $nested = $null
                try {
                    $nested = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Read)
                    if ((Get-MsixPayloadHash $nested) -cne $payloadHash) { throw 'Store upload does not contain the tested MSIX payload.' }
                } finally { if ($nested) { $nested.Dispose() }; $stream.Dispose() }
            } finally { $archive.Dispose() }
        }
    }
    return $manifest
}

function Assert-MsixCIOwnership([string] $Path, [string] $PackageFullName) {
    # Formal identities are allowed only on disposable hosted CI and only after the
    # installed runner recorded a new registration. Local daily-install guards stay intact.
    if ($env:GITHUB_ACTIONS -cne 'true' -or $env:RUNNER_ENVIRONMENT -cne 'github-hosted') {
        throw 'Formal MSIX acceptance requires a disposable GitHub-hosted runner.'
    }
    $relative = [IO.Path]::GetRelativePath([IO.Path]::GetTempPath(), [IO.Path]::GetFullPath($Path))
    if ($relative -notmatch '^VirtualPaper\.MsixAcceptance-[0-9a-f]{32}[\\/]ci-ownership\.json$') {
        throw 'MSIX ownership evidence must belong to the current isolated acceptance directory.'
    }
    $owner = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($owner.PackageFullName -cne $PackageFullName -or $owner.CreatedRegistration -ne $true) {
        throw 'The MSIX registration was not created by this acceptance session.'
    }
    $artifact = Get-PackageArtifact -ManifestPath $owner.ArtifactManifest -Channel MSIX
    if ($artifact.PackageSha256 -cne $owner.PackageSha256) { throw 'MSIX ownership artifact changed.' }
    return $artifact
}
