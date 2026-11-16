param(
    [Parameter(Mandatory)][string]$SettingsPath,
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
$settings = Get-Content -LiteralPath $SettingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
$builtInDirectory = [string]$settings.BuiltInWallpapersDir
if (-not [string]::IsNullOrWhiteSpace($builtInDirectory)) {
    if (-not [IO.Path]::IsPathRooted($builtInDirectory)) { throw 'The saved built-in wallpaper directory must be absolute.' }
    $builtInDirectory = [IO.Path]::GetFullPath($builtInDirectory)
} elseif (-not [string]::IsNullOrWhiteSpace($settings.WallpaperDir)) {
    $wallpaperDir = [string]$settings.WallpaperDir
    if (-not [IO.Path]::IsPathRooted($wallpaperDir)) { throw 'The saved wallpaper directory must be absolute.' }
    $wallpaperDir = [IO.Path]::GetFullPath($wallpaperDir).TrimEnd('\', '/')
    $libraryRoot = [IO.Path]::GetDirectoryName($wallpaperDir)
    if (-not $libraryRoot) { $libraryRoot = [IO.Path]::GetPathRoot([string]$settings.WallpaperDir) }
    $builtInDirectory = Join-Path $libraryRoot 'BuiltInWallpapers'
}
if (-not [string]::IsNullOrWhiteSpace($builtInDirectory)) {
    [IO.File]::WriteAllText($OutputPath, $builtInDirectory, [Text.UTF8Encoding]::new($true))
}
