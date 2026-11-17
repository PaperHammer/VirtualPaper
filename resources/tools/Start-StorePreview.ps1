#Requires -Version 7.2
[CmdletBinding()]
param(
    [string] $Locale = 'en-US',
    [string] $OutputRoot = 'TestResults/store-preview',
    [string] $CoreExe
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$catalog = Get-Content (Join-Path $repository 'localization/LanguageCatalog.json') -Raw | ConvertFrom-Json
if ($Locale -cnotin $catalog.Languages.Code) { throw "Unsupported locale: $Locale" }
if (-not $CoreExe) {
    $CoreExe = Join-Path $repository 'src/VirtualPaper/bin/Release/net10.0-windows10.0.19041.0/VirtualPaper.exe'
}
$CoreExe = [IO.Path]::GetFullPath($CoreExe)
if (-not (Test-Path -LiteralPath $CoreExe)) { throw 'Build the complete Release solution before preparing screenshots.' }
if (-not (Test-Path -LiteralPath (Join-Path (Split-Path -Parent $CoreExe) 'Plugins/UI/VirtualPaper.UI.exe'))) {
    throw 'The matching production UI must be deployed next to Core.'
}
# Each launch owns a fresh profile and named-pipe/mutex suffix. Never use a daily profile.
$session = [guid]::NewGuid().ToString('N')
$root = Join-Path ([IO.Path]::GetFullPath($OutputRoot)) $session
$library = Join-Path $root 'Library/wallpapers'
$builtIn = Join-Path $root 'Library/BuiltInWallpapers'
$null = New-Item -ItemType Directory -Path $library, $builtIn -Force
Copy-Item -Path (Join-Path $repository 'resources/BuiltInWallpapers/*') -Destination $builtIn -Recurse
@{
    IsFirstRun = $true; IsUpdated = $false; IsAutoStart = $false
    IsScreenSaverOn = $false; IsRunningLock = $false; Language = $Locale
    AiUseNoticeVersion = 1; ApplicationTheme = 1; SystemBackdrop = 0
    WallpaperDir = $library; BuiltInWallpapersDir = $builtIn
    NavigationAnimationsEnabled = $true; UiHardwareAccelerationEnabled = $true
} | ConvertTo-Json | Set-Content (Join-Path $root 'user_settings.json') -Encoding utf8NoBOM
foreach ($file in @('app_rules.json', 'wallpaper_layout.json')) {
    '[]' | Set-Content (Join-Path $root $file) -Encoding utf8NoBOM
}
$projects = Join-Path $root 'Projects'
$web = Join-Path $projects 'Luminous Flow'
$null = New-Item -ItemType Directory -Path $web -Force
foreach ($file in @('index.html', 'project.json', 'preview.gif')) {
    Copy-Item -LiteralPath (Join-Path $builtIn "vp-luminous-flow/$file") -Destination $web
}
@{ version = 1; name = 'Luminous Flow'; files = @(
    @{ path = 'index.html'; type = 'html'; role = 'entry' },
    @{ path = 'project.json'; type = 'json'; role = 'metadata' },
    @{ path = 'preview.gif'; type = 'image'; role = 'asset' }
) } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $web 'Luminous Flow.vpw') -Encoding utf8NoBOM
$demoDate = Get-Date -Format yyyy-MM-dd
@(
    @{ Type = 'FImage'; FileName = 'Emerald Morning'; FilePath = (Join-Path $builtIn 'vp-emerald-morning/vp-emerald-morning.png'); DateTime = $demoDate },
    @{ Type = 'FWebDesign'; FileName = 'Luminous Flow'; FilePath = (Join-Path $web 'Luminous Flow.vpw'); DateTime = $demoDate }
) | ConvertTo-Json | Set-Content (Join-Path $root 'recent_used.json') -Encoding utf8NoBOM
$start = [Diagnostics.ProcessStartInfo]::new($CoreExe)
$start.UseShellExecute = $false
$start.WorkingDirectory = Split-Path -Parent $CoreExe
$start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$start.Environment['VIRTUALPAPER_TEST_SESSION'] = $session
$start.Environment['VIRTUALPAPER_TEST_DATA_ROOT'] = $root
$process = [Diagnostics.Process]::Start($start)
$result = [ordered]@{ Session = $session; Root = $root; CoreId = $process.Id; Locale = $Locale
    SourceSha = (git -C $repository rev-parse HEAD); CoreSha256 = (Get-FileHash $CoreExe).Hash }
$result | ConvertTo-Json | Set-Content (Join-Path $root 'preview-session.json') -Encoding utf8NoBOM
$result
