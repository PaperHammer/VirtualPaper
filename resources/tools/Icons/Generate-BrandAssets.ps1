#Requires -Version 7.2
# Render branding and ICOs with a minimum frame size of 32 px.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'BrandAssetDrawing.ps1')
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
function RepoPath([string] $Path) { Join-Path $repository $Path }
& (Join-Path $PSScriptRoot 'Generate-StoreIcons.ps1')
$icon = Read-BrandSvg (RepoPath 'resources/icons/virtualpaper.svg')
$tile = Read-BrandSvg (RepoPath 'resources/store/artwork/app-icon-tile.svg')
$virtual = Read-BrandSvg (RepoPath 'resources/logos/source/virtual.svg')
$paper = Read-BrandSvg (RepoPath 'resources/logos/source/paper.svg')
$layouts = Get-Content (RepoPath 'resources/logos/source/layouts.json') -Raw | ConvertFrom-Json

function New-BrandLogo($Layout, [bool] $Rounded) {
    $drawing = [Windows.Media.DrawingGroup]::new()
    $context = $drawing.Open()
    try {
        $rectangle = [Windows.Rect]::new(0, 0, $Layout.width, $Layout.height)
        if ($Rounded) {
            $context.PushClip([Windows.Media.RectangleGeometry]::new($rectangle, 40, 40))
        }
        if (-not $Layout.transparent) {
            $background = [Windows.Media.LinearGradientBrush]::new(
                [Windows.Media.ColorConverter]::ConvertFromString('#E7F0FD'),
                [Windows.Media.ColorConverter]::ConvertFromString('#ACCBEE'), 90)
            $context.DrawRectangle($background, $null, $rectangle)
        }
        Draw-BrandFit $context $icon $Layout.icon
        Draw-BrandFit $context $virtual $Layout.virtual
        Draw-BrandFit $context $paper $Layout.paper
        if ($Rounded) { $context.Pop() }
    }
    finally { $context.Close() }
    $drawing.Freeze()
    return $drawing
}

Export-BrandBitmap (Get-BrandBitmap $icon 640 640 640 640) (RepoPath 'resources/icons/virtualpaper-icon_640.png')
Export-BrandBitmap (Get-BrandBitmap $icon 640 640 512 512) (RepoPath 'src/VirtualPaper.UI/Assets/icon-vp-512.png')
$icoPath = RepoPath 'resources/icons/virtualpaper.ico'
Export-BrandIco $icon $icoPath
foreach ($path in @('src/VirtualPaper/virtualpaper.ico', 'src/VirtualPaper.UI/Assets/virtualpaper.ico',
    'src/VirtualPaper.PlayerWeb/virtualpaper.ico', 'src/VirtualPaper.IntelligentPanel/Views/Comp/favicon.ico',
    'packaging/inno/Icons/virtualpaper.ico')) {
    Copy-Item -LiteralPath $icoPath -Destination (RepoPath $path)
}

# Keep the download indicator distinct from the ordinary application icon.
$download = [Windows.Media.DrawingGroup]::new()
$context = $download.Open()
$context.DrawDrawing($icon)
$context.DrawEllipse([Windows.Media.SolidColorBrush]::new([Windows.Media.ColorConverter]::ConvertFromString('#13BFAD')),
    $null, [Windows.Point]::new(466, 505), 113, 113)
$pen = [Windows.Media.Pen]::new([Windows.Media.Brushes]::White, 18)
$pen.StartLineCap = $pen.EndLineCap = [Windows.Media.PenLineCap]::Round
$pen.LineJoin = [Windows.Media.PenLineJoin]::Round
$context.DrawGeometry($null, $pen, [Windows.Media.Geometry]::Parse('M466 448 L466 550 M428 514 L466 552 L504 514'))
$context.Close(); $download.Freeze()
Export-BrandBitmap (Get-BrandBitmap $download 640 640 640 640) (RepoPath 'resources/icons/appicon_download.png')
Export-BrandIco $download (RepoPath 'resources/icons/appicon_download.ico')
foreach ($extension in @('png', 'ico')) {
    Copy-Item -LiteralPath (RepoPath "resources/icons/appicon_download.$extension") -Destination (RepoPath "src/VirtualPaper/Resources/appicon_download.$extension")
}

$logos = @{}
foreach ($layout in $layouts) {
    foreach ($rounded in @($false, $true)) {
        $name = if ($rounded) { $layout.file -replace '\.png$', '_with_round40.png' } else { $layout.file }
        $drawing = New-BrandLogo $layout $rounded
        Export-BrandBitmap (Get-BrandBitmap $drawing $layout.width $layout.height $layout.width $layout.height) (RepoPath "resources/logos/$name")
        if (-not $rounded) { $logos[$layout.file] = $drawing }
    }
}
Copy-Item -LiteralPath (RepoPath 'resources/logos/splashCol_1400x1400_with_round40.png') -Destination (RepoPath 'src/VirtualPaper.UI/Assets/splashCol_1400x1400_with_round40.png')
$wide = $logos['virtualpaper-logo-left_4216x2040.png']
Export-BrandBitmap (Get-BrandBitmap $wide 4216 2040 620 300) (RepoPath 'src/VirtualPaper/Resources/splash.png')
foreach ($entry in @(
    @('LockScreenLogo.scale-200.png', 48),
    @('StoreLogo.scale-200.png', 100),
    @('Square44x44Logo.scale-200.png', 88),
    @('Square44x44Logo.targetsize-32_altform-unplated.png', 32)
)) {
    Export-BrandBitmap (Get-BrandSystemIconBitmap $icon $entry[1]) (RepoPath "src/VirtualPaper.UI/Assets/$($entry[0])")
}
foreach ($entry in @(
    @('Square71x71Logo.scale-200.png', 142),
    @('Square150x150Logo.scale-200.png', 300),
    @('Square310x310Logo.scale-200.png', 620)
)) {
    Export-BrandBitmap (Get-BrandBitmap $tile 640 640 $entry[1] $entry[1]) (RepoPath "src/VirtualPaper.UI/Assets/$($entry[0])")
}
Export-BrandBitmap (Get-BrandBitmap $wide 4216 2040 620 300) (RepoPath 'src/VirtualPaper.UI/Assets/Wide310x150Logo.scale-200.png')
Export-BrandBitmap (Get-BrandBitmap $wide 4216 2040 1240 600) (RepoPath 'src/VirtualPaper.UI/Assets/SplashScreen.scale-200.png')

$small = [Windows.Media.DrawingGroup]::new()
$context = $small.Open()
$context.DrawRectangle([Windows.Media.SolidColorBrush]::new([Windows.Media.ColorConverter]::ConvertFromString('#E7F0FD')), $null, [Windows.Rect]::new(0, 0, 640, 640))
$context.DrawDrawing($tile)
$context.Close()
Export-BrandBitmap (Get-BrandBitmap $small 640 640 55 55) (RepoPath 'packaging/inno/Themes/wizard_small.bmp')
$vertical = [Windows.Media.DrawingGroup]::new()
$context = $vertical.Open()
$context.PushTransform([Windows.Media.MatrixTransform]::new([Windows.Media.Matrix]::new(0, 1, -1, 0, 2040, 0)))
$context.DrawDrawing($wide)
$context.Pop(); $context.Close()
Export-BrandBitmap (Get-BrandBitmap $vertical 2040 4216 187 386) (RepoPath 'packaging/inno/Themes/wizard_large.bmp')

# Refresh only the brand image; the two real README captures remain unchanged.
$heroPath = RepoPath 'resources/readme/hero.svg'
$hero = [IO.File]::ReadAllText($heroPath)
$iconData = [Convert]::ToBase64String([IO.File]::ReadAllBytes((RepoPath 'src/VirtualPaper.UI/Assets/icon-vp-512.png')))
$pattern = '(<image x="83" y="65" width="112" height="112" href="data:image/png;base64,)[^"]+'
if ([regex]::Matches($hero, $pattern).Count -ne 1) { throw 'Cannot locate the unique README header icon.' }
$hero = [regex]::Replace($hero, $pattern, [Text.RegularExpressions.MatchEvaluator]{ param($match) $match.Groups[1].Value + $iconData })
[IO.File]::WriteAllText($heroPath, $hero, [Text.UTF8Encoding]::new($false))
Write-Host '[OK] Application, installer, MSIX, download, splash, logo and README assets regenerated.'
