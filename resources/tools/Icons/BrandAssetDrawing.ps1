# Shared renderer for the repository's path/gradient SVG branding sources.
# Deliberately limited to these self-contained assets, not a general SVG reader.
Add-Type -AssemblyName PresentationCore, WindowsBase

function Get-BrandRuntimeIconSizes { return @(32, 40, 48, 64, 128, 256) }

function Get-BrandBrush([string] $Fill, [xml] $Svg) {
    if ($Fill -notmatch '^url\(#(.+)\)$') {
        return [Windows.Media.SolidColorBrush]::new([Windows.Media.ColorConverter]::ConvertFromString($Fill))
    }
    $gradient = $Svg.SelectSingleNode("//*[@id='$($Matches[1])']")
    if (-not $gradient) { throw "Missing gradient: $Fill" }
    $brush = [Windows.Media.LinearGradientBrush]::new()
    if ($gradient.gradientUnits -eq 'userSpaceOnUse') { $brush.MappingMode = [Windows.Media.BrushMappingMode]::Absolute }
    $brush.StartPoint = [Windows.Point]::new([double]$gradient.x1, [double]$gradient.y1)
    $brush.EndPoint = [Windows.Point]::new([double]$gradient.x2, [double]$gradient.y2)
    foreach ($stop in $gradient.ChildNodes | Where-Object LocalName -EQ 'stop') {
        $brush.GradientStops.Add([Windows.Media.GradientStop]::new(
            [Windows.Media.ColorConverter]::ConvertFromString($stop.'stop-color'), [double]$stop.offset))
    }
    return $brush
}

function Read-BrandSvg([string] $SvgPath) {
    [xml]$svg = [IO.File]::ReadAllText($SvgPath)
    $drawing = [Windows.Media.DrawingGroup]::new()
    $context = $drawing.Open()
    try {
        foreach ($rect in $svg.DocumentElement.SelectNodes("*[local-name()='rect']")) {
            $context.DrawRoundedRectangle((Get-BrandBrush $rect.fill $svg), $null,
                [Windows.Rect]::new(0, 0, [double]$rect.width, [double]$rect.height), [double]$rect.rx, [double]$rect.ry)
        }
        $group = $svg.DocumentElement.SelectSingleNode("*[local-name()='g']")
        if ($group) {
            $values = @([regex]::Matches($group.transform, '[-\d.]+') | ForEach-Object {
                [double]::Parse($_.Value, [Globalization.CultureInfo]::InvariantCulture)
            })
            if ($values.Count -ne 6) { throw "Expected a matrix transform in $SvgPath" }
            $context.PushTransform([Windows.Media.MatrixTransform]::new([Windows.Media.Matrix]::new(
                $values[0], $values[1], $values[2], $values[3], $values[4], $values[5])))
            $outline = $svg.SelectSingleNode("//*[local-name()='clipPath']/*[local-name()='path']")
            $context.PushClip([Windows.Media.Geometry]::Parse($outline.d))
            $paths = $group.SelectNodes(".//*[local-name()='path']")
        }
        else { $paths = $svg.DocumentElement.SelectNodes("*[local-name()='path']") }
        foreach ($path in $paths) {
            $data = if ($path.'fill-rule' -eq 'evenodd') { "F0 $($path.d)" } else { $path.d }
            $context.DrawGeometry((Get-BrandBrush $path.fill $svg), $null, [Windows.Media.Geometry]::Parse($data))
        }
        if ($group) { $context.Pop(); $context.Pop() }
    }
    finally { $context.Close() }
    $drawing.Freeze()
    return $drawing
}

function Draw-BrandFit($Context, $Drawing, [double[]] $Rectangle) {
    $bounds = $Drawing.Bounds
    $scale = [Math]::Min($Rectangle[2] / $bounds.Width, $Rectangle[3] / $bounds.Height)
    $x = $Rectangle[0] + ($Rectangle[2] - $bounds.Width * $scale) / 2 - $bounds.X * $scale
    $y = $Rectangle[1] + ($Rectangle[3] - $bounds.Height * $scale) / 2 - $bounds.Y * $scale
    $Context.PushTransform([Windows.Media.MatrixTransform]::new([Windows.Media.Matrix]::new($scale, 0, 0, $scale, $x, $y)))
    $Context.DrawDrawing($Drawing)
    $Context.Pop()
}

function Get-BrandBitmap($Drawing, [double] $SourceWidth, [double] $SourceHeight, [int] $Width, [int] $Height) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    $context.PushTransform([Windows.Media.ScaleTransform]::new($Width / $SourceWidth, $Height / $SourceHeight))
    $context.DrawDrawing($Drawing)
    $context.Pop()
    $context.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($Width, $Height, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $bitmap.Freeze()
    return $bitmap
}

function Export-BrandBitmap($Bitmap, [string] $Path) {
    $encoder = if ([IO.Path]::GetExtension($Path) -eq '.bmp') {
        [Windows.Media.Imaging.BmpBitmapEncoder]::new()
    } else { [Windows.Media.Imaging.PngBitmapEncoder]::new() }
    if ($encoder -is [Windows.Media.Imaging.BmpBitmapEncoder]) {
        $Bitmap = [Windows.Media.Imaging.FormatConvertedBitmap]::new($Bitmap, [Windows.Media.PixelFormats]::Bgr24, $null, 0)
    }
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($Bitmap))
    $stream = [IO.File]::Create($Path)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}

function Get-BrandSystemIconBitmap($Drawing, [int] $Size) {
    # Fit both ICO frames and manifest PNG icons to the visible artwork.
    $padding = if ($Size -le 32) { 0.25 } else { [Math]::Max(0.5, $Size * 0.02) }
    $artwork = [Windows.Media.DrawingGroup]::new()
    $context = $artwork.Open()
    try { Draw-BrandFit $context $Drawing @($padding, $padding, ($Size - 2 * $padding), ($Size - 2 * $padding)) }
    finally { $context.Close() }
    return Get-BrandBitmap $artwork $Size $Size $Size $Size
}

function Export-BrandIco($Drawing, [string] $Path) {
    $sizes = @(Get-BrandRuntimeIconSizes)
    $frames = foreach ($size in $sizes) {
        $bitmap = Get-BrandSystemIconBitmap $Drawing $size
        $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
        $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
        $stream = [IO.MemoryStream]::new()
        try { $encoder.Save($stream); ,$stream.ToArray() } finally { $stream.Dispose() }
    }
    $stream = [IO.File]::Create($Path)
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($i = 0; $i -lt $sizes.Count; $i++) {
            $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
            $offset += $frames[$i].Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
    }
    finally { $writer.Dispose(); $stream.Dispose() }
}
