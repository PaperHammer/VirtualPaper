Add-Type -AssemblyName System.Drawing

$src = Read-Host "Source PNG path"
$dst = Read-Host "Destination ICO path (default: same name + .ico)"

if (-not $dst) {
    $dst = [System.IO.Path]::ChangeExtension($src, '.ico')
}

if (-not (Test-Path $src)) {
    Write-Host "ERROR: Source file not found: $src" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Source : $src"
Write-Host "Target : $dst"
Write-Host ""

$srcImg = [System.Drawing.Image]::FromFile($src)
Write-Host "Input  : $($srcImg.Width)x$($srcImg.Height)"
$sizes = @(16, 24, 32, 48, 64, 128, 256, 512)

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)

# ICO header: reserved(2) + type(2) + count(2)
$bw.Write([uint16]0)  # reserved
$bw.Write([uint16]1)  # type: ICO
$bw.Write([uint16]$sizes.Count)  # image count

$offset = 6 + ($sizes.Count * 16)
$imageDatas = @()

foreach ($s in $sizes) {
    $sz = if ($s -ge 256) { 0 } else { $s }
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($srcImg, 0, 0, $s, $s)
    $g.Dispose()

    $mss = New-Object System.IO.MemoryStream
    $bmp.Save($mss, [System.Drawing.Imaging.ImageFormat]::Png)
    $data = $mss.ToArray()
    $mss.Dispose()

    # DIRENTRY: w(4) + h(1) + colors(1) + reserved(2) + planes(2) + bpp(2) + size(4) + offset(4)
    $bw.Write([byte]$sz)      # width
    $bw.Write([byte]$sz)      # height
    $bw.Write([byte]0)        # color palette
    $bw.Write([byte]0)        # reserved
    $bw.Write([uint16]0)      # planes
    $bw.Write([uint16]32)     # bpp
    $bw.Write([uint32]$data.Length)  # size
    $bw.Write([uint32]$offset)       # offset

    $imageDatas += $data
    $offset += $data.Length
    $bmp.Dispose()
}

foreach ($data in $imageDatas) {
    $bw.Write($data)
}

$bw.Flush()
[System.IO.File]::WriteAllBytes($dst, $ms.ToArray())
$bw.Dispose()
$ms.Dispose()
$srcImg.Dispose()

Write-Host ""
Write-Host "Done: $dst" -ForegroundColor Green
Write-Host "Size: $([math]::Round($info.Length / 1024, 1)) KB"
Write-Host "Resolutions: $($sizes -join ', ')"
