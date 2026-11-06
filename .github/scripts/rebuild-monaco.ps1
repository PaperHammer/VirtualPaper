#requires -Version 7.2
param(
    [string]$WorkDirectory = (Join-Path $PSScriptRoot "../../TestResults/monaco-rebuild-$([guid]::NewGuid().ToString('N'))")
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Rebuild the existing Monaco API with a patched sanitizer, using upstream's
# locked build tools and AMD configuration. Never patch minified JavaScript.
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$work = [IO.Path]::GetFullPath($WorkDirectory)
if (Test-Path -LiteralPath $work) { throw 'WorkDirectory must be new.' }
[void](New-Item -ItemType Directory -Path $work)
$sourceArchive = Join-Path $work 'monaco-source.tgz'
Invoke-WebRequest 'https://codeload.github.com/microsoft/monaco-editor/tar.gz/refs/tags/v0.55.1' -OutFile $sourceArchive
if ((Get-FileHash -LiteralPath $sourceArchive -Algorithm SHA256).Hash -cne 'C563A0525C621F873AE70F6FF4143FC983E3C93380039BC9D86DE37E31229FD9') {
    throw 'Monaco source archive checksum mismatch.'
}
& tar -xf $sourceArchive -C $work
if ($LASTEXITCODE -ne 0) { throw 'Cannot extract Monaco sources.' }
$source = Join-Path $work 'monaco-editor-0.55.1'

$purifyArchive = Join-Path $work 'dompurify.tgz'
Invoke-WebRequest 'https://registry.npmjs.org/dompurify/-/dompurify-3.4.17.tgz' -OutFile $purifyArchive
$purifyHash = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($purifyArchive)))
if ($purifyHash -cne 'B/pSlYVHQ58kqISD/yDjuJ1yfzI34m1gF4lAod2bwSRhiN4K5Cl3H8Af7hqB2PmTL911tdEudnnXn5FnhRoz0w==') {
    throw 'DOMPurify package integrity mismatch.'
}
$purify = Join-Path $work 'dompurify'
[void](New-Item -ItemType Directory -Path $purify)
& tar -xf $purifyArchive -C $purify
if ($LASTEXITCODE -ne 0) { throw 'Cannot extract DOMPurify.' }

Push-Location -LiteralPath $source
try {
    & npm.cmd ci --ignore-scripts --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'Monaco dependency restore failed.' }
    # The core package vendors DOMPurify as a standalone ESM module. Replacing
    # that build input updates the code actually bundled into the AMD output.
    Copy-Item -LiteralPath (Join-Path $purify 'package/dist/purify.es.mjs') `
        -Destination 'node_modules/monaco-editor-core/esm/vs/base/browser/dompurify/dompurify.js'
    Push-Location 'monaco-lsp-client'
    try {
        & npm.cmd ci --ignore-scripts --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'LSP dependency restore failed.' }
        & npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw 'LSP build failed.' }
    } finally { Pop-Location }
    Push-Location 'build/amd'
    try {
        & ../../node_modules/.bin/vite.cmd build
        if ($LASTEXITCODE -ne 0) { throw 'Monaco AMD build failed.' }
    } finally { Pop-Location }
} finally { Pop-Location }

$output = Join-Path $work 'runtime/package'
[void](New-Item -ItemType Directory -Path $output -Force)
Copy-Item -LiteralPath (Join-Path $source 'out/monaco-editor/min') -Destination $output -Recurse
$sanitizerBundles = @(Get-ChildItem -LiteralPath (Join-Path $output 'min/vs') -Filter '*.js' -File |
    Where-Object { [IO.File]::ReadAllText($_.FullName) -match '\.version="3\.4\.17"' })
if ($sanitizerBundles.Count -ne 1) { throw 'Expected exactly one bundle containing DOMPurify 3.4.17.' }

$trackedPackage = Join-Path $repositoryRoot 'src/WebBackdrop/Assets/monaco-editor-0.55.1/package'
foreach ($file in @('LICENSE', 'ThirdPartyNotices.txt', 'package.json')) {
    Copy-Item -LiteralPath (Join-Path $trackedPackage $file) -Destination $output
}
Copy-Item -LiteralPath (Join-Path $purify 'package/LICENSE') -Destination (Join-Path $output 'DOMPurify.LICENSE')
$metadataPath = Join-Path $output 'package.json'
$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
$metadata.dependencies.dompurify = '3.4.17'
[IO.File]::WriteAllText($metadataPath, ($metadata | ConvertTo-Json -Depth 20) + "`n", [Text.UTF8Encoding]::new($false))
Write-Output "Patched runtime staged at: $output"
Write-Output 'Replace the tracked min directory as a whole; do not merge it with old hashed bundles.'
