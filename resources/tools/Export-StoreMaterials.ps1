#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string] $Version,
    [switch] $InitialRelease,
    [ValidatePattern('^[0-9a-f]{40}$')][string] $SourceSha,
    [string] $MaterialsRoot = (Join-Path $PSScriptRoot '../store'),
    [string] $OutputRoot = 'TestResults/store-materials'
)
$ErrorActionPreference = 'Stop'
$materials = [IO.Path]::GetFullPath($MaterialsRoot)
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $SourceSha) { $SourceSha = (git -C $repository rev-parse HEAD).Trim() }
$languages = (Get-Content (Join-Path $repository 'localization/LanguageCatalog.json') -Raw | ConvertFrom-Json).Languages.Code
$release = Get-Content (Join-Path $materials "releases/$Version.json") -Raw | ConvertFrom-Json -AsHashtable
$index = Get-Content (Join-Path $materials 'screenshots/index.json') -Raw | ConvertFrom-Json -AsHashtable
if ($index.FormatVersion -ne 1 -or $index.SourceSha -notmatch '^[0-9a-f]{40}$' -or $index.CursorIncluded -cne $false) {
    throw 'Screenshot provenance must identify the source and confirm pointer-free captures.'
}
if (@(Compare-Object @($languages | Sort-Object) @($release.Keys | Sort-Object)).Count) { throw 'Release-note languages differ from the catalog.' }
$listingFiles = @(Get-ChildItem (Join-Path $materials 'listings') -Filter '*.json')
if (@(Compare-Object @($languages | Sort-Object) @($listingFiles.BaseName | Sort-Object)).Count) { throw 'Listing languages differ from the catalog.' }
function Assert-Text($Text, [int] $Limit, [string] $Label) {
    if ($Text -isnot [string] -or [string]::IsNullOrWhiteSpace($Text) -or $Text.Length -gt $Limit -or
        $Text -match '(?i)\b(TODO|TBD|FIXME)\b|\uFFFD') { throw "Invalid or overlong text: $Label" }
}
function Resolve-Image([string] $Relative) {
    if ($Relative -notmatch '^[a-z]{2}-[A-Z]{2}/[0-9]{2}-[a-z-]+\.png$') { throw "Invalid screenshot path: $Relative" }
    $path = Join-Path $materials "screenshots/$Relative"
    $bytes = [IO.File]::ReadAllBytes($path)
    if ($bytes.Length -lt 45 -or $bytes.Length -gt 50MB -or [Convert]::ToHexString($bytes[0..7]) -cne '89504E470D0A1A0A' -or
        [Text.Encoding]::ASCII.GetString($bytes, 12, 4) -cne 'IHDR' -or
        [Convert]::ToHexString($bytes[($bytes.Length - 12)..($bytes.Length - 1)]) -cne '0000000049454E44AE426082') {
        throw "Invalid PNG screenshot: $Relative"
    }
    $width = [uint32]$bytes[16] * 16777216 + [uint32]$bytes[17] * 65536 + [uint32]$bytes[18] * 256 + $bytes[19]
    $height = [uint32]$bytes[20] * 16777216 + [uint32]$bytes[21] * 65536 + [uint32]$bytes[22] * 256 + $bytes[23]
    if ($width -lt 1366 -or $height -lt 768) { throw "Desktop screenshot is too small: $Relative ($width x $height)" }
    return @{ Path = $path; Width = $width; Height = $height; Sha256 = (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() }
}
$listings = [ordered]@{}
$images = @{}
$readable = [ordered]@{}
foreach ($locale in $languages) {
    $copy = Get-Content (Join-Path $materials "listings/$locale.json") -Raw | ConvertFrom-Json -AsHashtable
    Assert-Text $copy.shortDescription 1000 "$locale shortDescription"
    Assert-Text $copy.description 10000 "$locale description"
    Assert-Text $release[$locale] 1500 "$locale release notes"
    if ($copy.features -isnot [array] -or $copy.features.Count -lt 1 -or $copy.features.Count -gt 20) { throw "Invalid features: $locale" }
    foreach ($feature in $copy.features) { Assert-Text $feature 200 "$locale feature" }
    if ($copy.keywords -isnot [array] -or $copy.keywords.Count -gt 7) { throw "Invalid keywords: $locale" }
    foreach ($keyword in $copy.keywords) { Assert-Text $keyword 40 "$locale keyword" }
    if ($copy.screenshots -isnot [array] -or $copy.screenshots.Count -lt 4 -or $copy.screenshots.Count -gt 10 -or
        @($copy.screenshots.id | Select-Object -Unique).Count -ne $copy.screenshots.Count) { throw "Invalid screenshot inventory: $locale" }
    $screenshots = foreach ($shot in $copy.screenshots) {
        Assert-Text $shot.caption 200 "$locale screenshot caption"
        if (-not $index.Scenes.ContainsKey($shot.id)) { throw "Unknown screenshot scene: $($shot.id)" }
        $scene = $index.Scenes[$shot.id]
        $relative = if ($scene.Locales.ContainsKey($locale)) { $scene.Locales[$locale] } else { $scene.Shared }
        if (-not $relative) { throw "Missing screenshot: $locale/$($shot.id)" }
        if (-not $images.ContainsKey($relative)) {
            $images[$relative] = Resolve-Image $relative
            $images[$relative].File = 'images/' + $relative.Substring(0, $relative.Length - 4) + '-' + $images[$relative].Sha256 + '.png'
        }
        [ordered]@{ fileName = $images[$relative].File; fileStatus = 'PendingUpload'; imageType = 'Screenshot'; description = $shot.caption }
    }
    $notes = if ($InitialRelease) { '' } else { $release[$locale] }
    $listings[$locale.ToLowerInvariant()] = [ordered]@{ baseListing = [ordered]@{
        shortDescription = $copy.shortDescription; description = $copy.description
        features = $copy.features; keywords = $copy.keywords; releaseNotes = $notes; images = @($screenshots)
    } }
    $readable[$locale] = "# Virtual Paper — $locale`n`n## Short description`n`n$($copy.shortDescription)`n`n## Description`n`n$($copy.description)`n`n## Features`n`n" +
        (($copy.features | ForEach-Object { "- $_" }) -join "`n") + "`n`n## Keywords`n`n$($copy.keywords -join ', ')`n`n## Screenshots`n`n" +
        (($screenshots | ForEach-Object { "![$($_.description)]($($_.fileName))`n`n$($_.description)" }) -join "`n`n") +
        "`n`n## Release notes — $Version`n`n$($release[$locale])`n"
}
# Validate all inputs before writing output. A fresh directory avoids mixing versions/stale images.
$destination = Join-Path ([IO.Path]::GetFullPath($OutputRoot)) "$Version-$([guid]::NewGuid().ToString('N'))"
$null = New-Item -ItemType Directory -Path $destination
foreach ($relative in $images.Keys) {
    $target = Join-Path $destination $images[$relative].File
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force
    Copy-Item -LiteralPath $images[$relative].Path -Destination $target
}
foreach ($locale in $languages) { $readable[$locale] | Set-Content (Join-Path $destination "$locale.md") -Encoding utf8NoBOM }
([ordered]@{ listings = $listings }) | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $destination 'listings.json') -Encoding utf8NoBOM
$manifest = [ordered]@{ FormatVersion = 1; SourceSha = $SourceSha; ProductVersion = $Version; InitialRelease = [bool]$InitialRelease
    ScreenshotSourceSha = $index.SourceSha; Locales = $languages; ImageCount = $images.Count
    ListingSha256 = (Get-FileHash (Join-Path $destination 'listings.json')).Hash.ToLowerInvariant()
    Files = @($images.Keys | Sort-Object -CaseSensitive | ForEach-Object { [ordered]@{ File = $images[$_].File; Sha256 = $images[$_].Sha256
        Width = $images[$_].Width; Height = $images[$_].Height } }) }
$digest = "$($manifest.ListingSha256)`n" + (($manifest.Files | ForEach-Object { "$($_.File):$($_.Sha256)" }) -join "`n")
$manifest.MaterialsSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($digest))).ToLowerInvariant()
$manifest | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $destination 'manifest.json') -Encoding utf8NoBOM
Write-Host "[OK] $($languages.Count) listings, $($images.Count) unique PNGs; offline output: $destination"
return $destination
