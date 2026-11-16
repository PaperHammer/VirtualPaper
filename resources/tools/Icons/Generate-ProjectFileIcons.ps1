#Requires -Version 7.2
# Render self-contained SVG paths; requires Windows and an STA PowerShell.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'BrandAssetDrawing.ps1')
$sizes = @(Get-BrandRuntimeIconSizes)
$iconRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../icons/projects'))
function New-Brush([string]$value, $document) {
    if (-not $value -or $value -eq 'none') { return $null }
    if ($value -match '^url\(#(.+)\)$') {
        $node = $document.SelectSingleNode("//*[@id='$($Matches[1])']")
        $brush = [Windows.Media.LinearGradientBrush]::new()
        $brush.StartPoint = [Windows.Point]::new([double]$node.x1, [double]$node.y1)
        $brush.EndPoint = [Windows.Point]::new([double]$node.x2, [double]$node.y2)
        foreach ($stop in $node.ChildNodes) {
            $brush.GradientStops.Add([Windows.Media.GradientStop]::new([Windows.Media.ColorConverter]::ConvertFromString($stop.'stop-color'), [double]$stop.offset))
        }
        return $brush
    }
    return [Windows.Media.SolidColorBrush]::new([Windows.Media.ColorConverter]::ConvertFromString($value))
}
foreach ($name in @('vpd', 'vpw')) {
    [xml]$svg = Get-Content -LiteralPath (Join-Path $iconRoot "$name.svg") -Raw
    $images = @()
    foreach ($size in $sizes) {
        $visual = [Windows.Media.DrawingVisual]::new()
        $context = $visual.RenderOpen()
        $context.PushTransform([Windows.Media.ScaleTransform]::new($size / 128.0, $size / 128.0))
        foreach ($path in $svg.DocumentElement.ChildNodes | Where-Object LocalName -EQ 'path') {
            $pen = $null
            if ($path.stroke -and $path.stroke -ne 'none') {
                $pen = [Windows.Media.Pen]::new((New-Brush $path.stroke $svg), [double]$path.'stroke-width')
                if ($path.'stroke-linecap' -eq 'round') { $pen.StartLineCap = $pen.EndLineCap = [Windows.Media.PenLineCap]::Round }
                if ($path.'stroke-linejoin' -eq 'round') { $pen.LineJoin = [Windows.Media.PenLineJoin]::Round }
            }
            $opacity = if ($path.opacity) { [double]$path.opacity } else { 1.0 }
            $context.PushOpacity($opacity)
            $context.DrawGeometry((New-Brush $path.fill $svg), $pen, [Windows.Media.Geometry]::Parse($path.d))
            $context.Pop()
        }
        $context.Pop(); $context.Close()
        $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
        $bitmap.Render($visual)
        $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
        $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
        $stream = [IO.MemoryStream]::new()
        try { $encoder.Save($stream); $images += ,$stream.ToArray() } finally { $stream.Dispose() }
    }
    [IO.File]::WriteAllBytes((Join-Path $iconRoot "$name.png"), $images[-1])
    $stream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
        $offset = 6 + 16 * $images.Count
        for ($i = 0; $i -lt $sizes.Count; $i++) {
            $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
            $offset += $images[$i].Length
        }
        foreach ($bytes in $images) { $writer.Write([byte[]]$bytes) }
        $writer.Flush()
        [IO.File]::WriteAllBytes((Join-Path $iconRoot "$name.ico"), $stream.ToArray())
    }
    finally { $writer.Dispose(); $stream.Dispose() }
    Write-Output "Generated $name PNG and minimum-32px ICO."
}
