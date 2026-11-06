#requires -Version 7.2
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/BuiltInWallpaperTestSupport.ps1"
function Assert-BuiltInRejected([scriptblock] $Action, [string] $Message) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    if (-not $rejected) { throw $Message }
}
$root = Join-Path ([IO.Path]::GetTempPath()) "VirtualPaper-built-in-contract-$([guid]::NewGuid().ToString('N'))"
try {
    $source = Join-Path $root 'package source'
    foreach ($name in @('first', 'second')) {
        $folder = New-Item -ItemType Directory -Path (Join-Path $source $name) -Force
        @{ Title = $name; FolderName = $name; FilePath = 'media.bin' } | ConvertTo-Json | Set-Content (Join-Path $folder 'wp_metadata_basic.json')
        "original $name" | Set-Content (Join-Path $folder 'media.bin')
        'CC0 source notice' | Set-Content (Join-Path $folder 'NOTICE.txt')
    }
    $state = New-BuiltInWallpaperAcceptance $source (Join-Path $root 'isolated profile')
    Assert-BuiltInRejected { Assert-InitialBuiltInWallpapers $state } 'Missing initial payload passed.'
    New-Item -ItemType Directory -Path (Split-Path $state.Destination -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $state.Destination -Recurse
    Assert-InitialBuiltInWallpapers $state
    @{ BuiltInWallpapersDir = $state.Destination } | ConvertTo-Json | Set-Content (Join-Path $state.DataRoot 'user_settings.json')
    Initialize-BuiltInWallpaperReinstallFixture $state
    Assert-BuiltInRejected { Assert-ReinstalledBuiltInWallpapers $state } 'A missing newly deployed folder passed.'
    Copy-Item -LiteralPath (Join-Path $source 'second') -Destination (Join-Path $state.Destination 'second') -Recurse
    Assert-ReinstalledBuiltInWallpapers $state
    $edited = Join-Path $state.Destination 'first/wp_metadata_basic.json'
    $saved = [IO.File]::ReadAllBytes($edited)
    Copy-Item -LiteralPath (Join-Path $source 'first/wp_metadata_basic.json') -Destination $edited -Force
    Assert-BuiltInRejected { Assert-ReinstalledBuiltInWallpapers $state } 'An overwritten user title passed.'
    [IO.File]::WriteAllBytes($edited, $saved)
    Copy-Item -LiteralPath (Join-Path $source 'first/NOTICE.txt') -Destination $state.DeletedFile
    Assert-BuiltInRejected { Assert-ReinstalledBuiltInWallpapers $state } 'A restored user-deleted file passed.'
    Remove-Item -LiteralPath $state.DeletedFile
    Assert-ReinstalledBuiltInWallpapers $state
    Assert-UninstalledBuiltInWallpapers $state
    Set-Content -LiteralPath (Join-Path $state.Destination 'first/my-note.txt') -Value 'corrupted'
    Assert-BuiltInRejected { Assert-UninstalledBuiltInWallpapers $state } 'Modified or lost user data after uninstall passed.'
    Write-Host '[OK] Built-in checks reject missing payloads, overwritten edits, restored user deletions and data loss.'
} finally {
    $resolved = [IO.Path]::GetFullPath($root)
    $temporary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($temporary, [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture cleanup escaped the temporary directory.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
