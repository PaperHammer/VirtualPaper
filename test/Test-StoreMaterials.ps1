#Requires -Version 7.2
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$fixture = Join-Path $repository "TestResults/store-material-contract-$([guid]::NewGuid().ToString('N'))"
$materials = Join-Path $fixture 'source'
$output = Join-Path $fixture 'output'
$version = (Get-ChildItem (Join-Path $repository 'resources/store/releases') -Filter '*.json' |
    Sort-Object { [version]$_.BaseName } -Descending | Select-Object -First 1).BaseName
$localeCount = (Get-Content (Join-Path $repository 'localization/LanguageCatalog.json') -Raw | ConvertFrom-Json).Languages.Count
$null = New-Item -ItemType Directory -Path $fixture
Copy-Item -LiteralPath (Join-Path $repository 'resources/store') -Destination $materials -Recurse
$export = Join-Path $repository 'resources/tools/Export-StoreMaterials.ps1'
$arguments = @{ Version = $version; MaterialsRoot = $materials; OutputRoot = $output }
function Require([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
function Reject([scriptblock] $Action) {
    $count = @(Get-ChildItem -LiteralPath $output -Directory).Count
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Require $rejected 'Invalid Store materials were accepted.'
    Require (@(Get-ChildItem -LiteralPath $output -Directory).Count -eq $count) 'Invalid input left a partial output.'
}
& $export @arguments -InitialRelease
$initial = Get-ChildItem -LiteralPath $output -Directory | Select-Object -First 1
$payload = Get-Content (Join-Path $initial.FullName 'listings.json') -Raw | ConvertFrom-Json -AsHashtable
Require ($payload.listings.Count -eq $localeCount) 'Export every supported language.'
foreach ($listing in $payload.listings.Values) {
    Require ($listing.baseListing.releaseNotes -ceq '') 'First Store submission must leave release notes blank.'
    Require (-not $listing.baseListing.ContainsKey('title')) 'Do not replace the reserved Store title.'
    foreach ($image in $listing.baseListing.images) {
        Require (Test-Path -LiteralPath (Join-Path $initial.FullName $image.fileName)) 'Every image reference must resolve in the handoff.'
        Require ($image.fileStatus -ceq 'PendingUpload' -and $image.imageType -ceq 'Screenshot') 'Export the MSIX image schema.'
    }
}
& $export @arguments
$updated = Get-ChildItem -LiteralPath $output -Directory | Where-Object FullName -CNE $initial.FullName | Select-Object -First 1
$update = Get-Content (Join-Path $updated.FullName 'listings.json') -Raw | ConvertFrom-Json -AsHashtable
$release = Get-Content (Join-Path $materials "releases/$version.json") -Raw | ConvertFrom-Json -AsHashtable
Require ($update.listings.'zh-cn'.baseListing.releaseNotes -ceq $release.'zh-CN') 'Use versioned release notes on updates.'
$manifest = Get-Content (Join-Path $updated.FullName 'manifest.json') -Raw | ConvertFrom-Json
Require ($manifest.ListingSha256 -ceq (Get-FileHash (Join-Path $updated.FullName 'listings.json')).Hash.ToLowerInvariant()) 'Bind listing copy to the manifest.'
foreach ($file in $manifest.Files) {
    Require ($file.Sha256 -ceq (Get-FileHash (Join-Path $updated.FullName $file.File)).Hash.ToLowerInvariant()) 'Bind screenshot bytes to the manifest.'
}
$repeat = & $export @arguments
$repeatManifest = Get-Content (Join-Path $repeat 'manifest.json') -Raw | ConvertFrom-Json
Require ($repeatManifest.MaterialsSha256 -ceq $manifest.MaterialsSha256) 'Identical inputs must have a stable retry digest.'
Require ($repeatManifest.Files[0].File -match '-[0-9a-f]{64}\.png$') 'Export content-addressed image names for Store updates.'
$path = Join-Path $materials 'listings/en-US.json'
$original = [IO.File]::ReadAllText($path)
foreach ($scenario in @('missing', 'overlong', 'duplicate', 'caption')) {
    $value = $original | ConvertFrom-Json -AsHashtable
    switch ($scenario) {
        missing { Move-Item -LiteralPath $path -Destination "$path.hidden" }
        overlong { $value.description = 'x' * 10001 }
        duplicate { $value.screenshots[1].id = $value.screenshots[0].id }
        caption { $value.screenshots[0].caption = 'x' * 201 }
    }
    if ($scenario -ne 'missing') { $value | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $path -Encoding utf8NoBOM }
    Reject { & $export @arguments }
    if ($scenario -eq 'missing') { Move-Item -LiteralPath "$path.hidden" -Destination $path }
    [IO.File]::WriteAllText($path, $original)
}
$indexPath = Join-Path $materials 'screenshots/index.json'
$indexOriginal = [IO.File]::ReadAllText($indexPath)
$withPointer = $indexOriginal | ConvertFrom-Json -AsHashtable
$withPointer.CursorIncluded = $true
$withPointer | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $indexPath -Encoding utf8NoBOM
Reject { & $export @arguments }
foreach ($invalid in @('../outside.png', 'en-US/missing.png')) {
    $index = $indexOriginal | ConvertFrom-Json -AsHashtable
    $index.Scenes.'01-library'.Locales.'en-US' = $invalid
    $index | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $indexPath -Encoding utf8NoBOM
    Reject { & $export @arguments }
}
[IO.File]::WriteAllText($indexPath, $indexOriginal)
$imagePath = Join-Path $materials 'screenshots/en-US/01-library.png'
$imageBytes = [IO.File]::ReadAllBytes($imagePath)
Copy-Item -LiteralPath (Join-Path $materials 'screenshots/en-US/04-image-editor.png') -Destination $imagePath -Force
$changedImageExport = & $export @arguments
$changedImageManifest = Get-Content (Join-Path $changedImageExport 'manifest.json') -Raw | ConvertFrom-Json
Require ($changedImageManifest.MaterialsSha256 -cne $manifest.MaterialsSha256) 'Image changes must change the retry digest.'
[IO.File]::WriteAllBytes($imagePath, $imageBytes)
[IO.File]::WriteAllBytes($imagePath, [byte[]]@(1, 2, 3))
Reject { & $export @arguments }
$small = [byte[]]$imageBytes.Clone()
# Width 100 in the PNG header; this must fail before creating output.
$small[16] = 0; $small[17] = 0; $small[18] = 0; $small[19] = 100
[IO.File]::WriteAllBytes($imagePath, $small)
Reject { & $export @arguments }
[IO.File]::WriteAllBytes($imagePath, $imageBytes)
Write-Host '[OK] Store materials: all locales, initial/update notes, real image references/hashes, missing/invalid text/images and no partial output.'
