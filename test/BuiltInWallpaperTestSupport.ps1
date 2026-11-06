# Checks real installer effects. This helper never deploys or overwrites wallpaper files.
function Get-BuiltInWallpaperHashes([string] $Root) {
    $hashes = @{}
    if (Test-Path -LiteralPath $Root) {
        foreach ($file in Get-ChildItem -LiteralPath $Root -File -Recurse) {
            $hashes[[IO.Path]::GetRelativePath($Root, $file.FullName)] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        }
    }
    return ,$hashes
}

function Assert-BuiltInWallpaperHashes([string] $Root, $Expected) {
    $actual = Get-BuiltInWallpaperHashes $Root
    if ($actual.Count -ne $Expected.Count) { throw "Built-in wallpaper file inventory changed: $Root" }
    foreach ($entry in $Expected.GetEnumerator()) {
        if ($actual[$entry.Key] -cne $entry.Value) { throw "Built-in wallpaper missing or changed: $($entry.Key)" }
    }
}

function New-BuiltInWallpaperAcceptance([string] $Source, [string] $DataRoot) {
    $catalog = @(Get-ChildItem -LiteralPath $Source -Directory | Where-Object {
        Test-Path -LiteralPath (Join-Path $_.FullName 'wp_metadata_basic.json')
    } | Sort-Object Name)
    if ($catalog.Count -lt 2) { throw 'Installer acceptance requires the actual built-in collection, with at least two items.' }
    $root = [IO.Path]::GetFullPath($DataRoot)
    @{
        Source = [IO.Path]::GetFullPath($Source); DataRoot = $root
        Destination = Join-Path $root 'Library/BuiltInWallpapers'
        OriginalDestination = Join-Path $root 'Library/BuiltInWallpapers'
        Catalog = $catalog.Name; Expected = Get-BuiltInWallpaperHashes $Source
        DeletedFile = $null
        Evidence = [ordered]@{ ItemCount = $catalog.Count; InitialDeployment = $false; MissingFolderRestored = $false
            ExistingDataPreserved = $false; SavedDirectoryHonored = $false; UninstallDataPreserved = $false }
    }
}

function Assert-InitialBuiltInWallpapers($State) {
    Assert-BuiltInWallpaperHashes $State.Destination $State.Expected
    $State.Evidence.InitialDeployment = $true
}

function Initialize-BuiltInWallpaperReinstallFixture($State) {
    if (-not $State.Evidence.InitialDeployment) { throw 'Initial built-in deployment was not verified.' }
    $root = [IO.Path]::GetFullPath($State.DataRoot).TrimEnd('\') + '\'
    $destination = [IO.Path]::GetFullPath($State.Destination)
    if (-not $destination.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'The built-in fixture escaped its isolated profile.' }
    # Model the user's saved independent directory, while Core is stopped.
    $moved = Join-Path $State.DataRoot 'moved built-in library'
    if (-not [IO.Path]::GetFullPath($moved).StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'The fixture destination escaped its isolated profile.' }
    if (Test-Path -LiteralPath $moved) { throw 'The relocation fixture already exists.' }
    Move-Item -LiteralPath $destination -Destination $moved
    $State.Destination = $moved
    $settingsPath = Join-Path $State.DataRoot 'user_settings.json'
    $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json -AsHashtable
    $settings.BuiltInWallpapersDir = $moved
    $settings | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $settingsPath -Encoding utf8

    $existing = Join-Path $moved $State.Catalog[0]
    $metadataPath = Join-Path $existing 'wp_metadata_basic.json'
    $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json -AsHashtable
    $metadata.Title = 'My edited built-in wallpaper'
    $metadata | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $metadataPath -Encoding utf8
    'User-owned note: preserve this file.' | Set-Content -LiteralPath (Join-Path $existing 'my-note.txt') -Encoding utf8
    # A whole existing folder must be skipped, including a file deliberately deleted by its user.
    $State.DeletedFile = Join-Path $existing 'NOTICE.txt'
    Remove-Item -LiteralPath $State.DeletedFile
    # A missing whole folder represents a new item added by a later package.
    # Move the owned fixture aside instead of deleting its media.
    $missing = Join-Path $moved $State.Catalog[-1]
    $archive = Join-Path $State.DataRoot 'missing-built-in-fixture'
    foreach ($path in @($missing, $archive)) {
        if (-not [IO.Path]::GetFullPath($path).StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'The missing-folder fixture escaped its isolated profile.' }
    }
    if (Test-Path -LiteralPath $archive) { throw 'The missing-folder fixture already exists.' }
    Move-Item -LiteralPath $missing -Destination $archive
    $State.Expected = Get-BuiltInWallpaperHashes $moved
    foreach ($entry in (Get-BuiltInWallpaperHashes $archive).GetEnumerator()) {
        $State.Expected[(Join-Path $State.Catalog[-1] $entry.Key)] = $entry.Value
    }
}

function Assert-ReinstalledBuiltInWallpapers($State) {
    Assert-BuiltInWallpaperHashes $State.Destination $State.Expected
    if (Test-Path -LiteralPath $State.DeletedFile) { throw 'Reinstall restored a file inside an existing user-edited wallpaper.' }
    if (Test-Path -LiteralPath $State.OriginalDestination) { throw 'Reinstall ignored the saved built-in library directory.' }
    $settings = Get-Content -LiteralPath (Join-Path $State.DataRoot 'user_settings.json') -Raw | ConvertFrom-Json
    if ($settings.BuiltInWallpapersDir -ine $State.Destination) { throw 'Reinstall changed the saved built-in directory.' }
    $State.Evidence.MissingFolderRestored = $true
    $State.Evidence.ExistingDataPreserved = $true
    $State.Evidence.SavedDirectoryHonored = $true
}

function Assert-UninstalledBuiltInWallpapers($State) {
    if (-not $State.Evidence.ExistingDataPreserved) { return }
    Assert-BuiltInWallpaperHashes $State.Destination $State.Expected
    $State.Evidence.UninstallDataPreserved = $true
}
