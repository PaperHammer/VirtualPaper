#Requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string] $PayloadRoot, [Parameter(Mandatory)][string] $BuildNumber,
    [AllowEmptyString()][string] $UpdatePlugins, [string] $OutputDirectory = '.')
$ErrorActionPreference = 'Stop'
if (-not $UpdatePlugins) { throw 'Plugin update must contain at least one plugin.' }
$payload = $PayloadRoot
. "$PSScriptRoot/ReleaseMetadata.ps1"
Assert-ReleaseBuildNumber $BuildNumber
$work = Join-Path $OutputDirectory 'plugin-payload'
[void](New-Item -ItemType Directory -Path $work -Force)
Copy-Item "$payload/app_comp_manifest.json" "$work/app_comp_manifest.json"
$manifest = Get-Content "$work/app_comp_manifest.json" -Raw | ConvertFrom-Json -AsHashtable
$pending = @{ plugins = [ordered]@{} }
$files = @("$work/app_comp_manifest.json", "$work/pending_update_plugins_manifest.json")
foreach ($plugin in ($UpdatePlugins -split ';')) {
    if ($plugin -notin @('UI', 'PlayerWeb', 'ScrSaver', 'ML', 'Shaders')) { throw "Invalid plugin: $plugin" }
    $name = "plugin-$($plugin.ToLowerInvariant())-$BuildNumber.zip"
    Compress-Archive -Force -LiteralPath "$payload/Plugins/$plugin" -DestinationPath "$work/$name"
    $pending.plugins[$plugin] = @{ build_number = $manifest.plugins[$plugin]; asset = $name
        sha256 = (Get-FileHash "$work/$name").Hash.ToLowerInvariant() }
    $files += "$work/$name"
}
$pending | ConvertTo-Json -Depth 10 | Set-Content "$work/pending_update_plugins_manifest.json" -Encoding utf8NoBOM
Compress-Archive -Force -LiteralPath $files -DestinationPath "$OutputDirectory/plugins_patch.zip"
# Existing clients request this exact spelling.
(Get-FileHash "$OutputDirectory/plugins_patch.zip").Hash.ToLowerInvariant() |
    Set-Content "$OutputDirectory/PLUGINS_PATCH_SHS256.txt" -Encoding utf8NoBOM
