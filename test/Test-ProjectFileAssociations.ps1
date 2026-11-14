#Requires -Version 7.2
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$iconRoot = Join-Path $repository 'resources/icons/projects'
foreach ($name in @('vpd', 'vpw')) {
    $bytes = [IO.File]::ReadAllBytes((Join-Path $iconRoot "$name.ico"))
    $reader = [IO.BinaryReader]::new([IO.MemoryStream]::new($bytes))
    try {
        if ($reader.ReadUInt16() -ne 0 -or $reader.ReadUInt16() -ne 1 -or $reader.ReadUInt16() -ne 7) { throw "Invalid ICO header: $name" }
        foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
            $dimension = if ($size -eq 256) { 0 } else { $size }
            if ($reader.ReadByte() -ne $dimension -or $reader.ReadByte() -ne $dimension) { throw "Missing ICO size $size in $name" }
            [void]$reader.ReadUInt16(); [void]$reader.ReadUInt16()
            if ($reader.ReadUInt16() -ne 32) { throw 'Project icons must have an alpha channel.' }
            $length = $reader.ReadUInt32(); $offset = $reader.ReadUInt32()
            if ($offset -lt 118 -or $length -eq 0 -or $offset + $length -gt $bytes.Length) { throw 'ICO frame points outside its file.' }
            if ([Convert]::ToHexString($bytes[$offset..($offset + 7)]) -cne '89504E470D0A1A0A') { throw 'ICO frame is not a PNG.' }
            $width = [byte[]]$bytes[($offset + 16)..($offset + 19)]; [Array]::Reverse($width)
            $height = [byte[]]$bytes[($offset + 20)..($offset + 23)]; [Array]::Reverse($height)
            if ([BitConverter]::ToUInt32($width, 0) -ne $size -or [BitConverter]::ToUInt32($height, 0) -ne $size) { throw 'ICO directory and image dimensions disagree.' }
        }
    } finally { $reader.Dispose() }
    [xml]$svg = Get-Content -LiteralPath (Join-Path $iconRoot "$name.svg") -Raw
    if ($svg.DocumentElement.viewBox -cne '0 0 128 128') { throw 'Project SVG icons must share their coordinate system.' }
}
if ((Get-FileHash (Join-Path $iconRoot 'vpd.png')).Hash -eq (Get-FileHash (Join-Path $iconRoot 'vpw.png')).Hash) {
    throw 'The two project types need distinguishable icons.'
}
[xml]$manifest = Get-Content (Join-Path $repository 'packaging/msix/Package.appxmanifest') -Raw
$associations = @($manifest.SelectNodes("//*[local-name()='FileTypeAssociation']"))
if ($associations.Count -ne 2 -or @($associations | Where-Object Parameters -CNE '--open-project "%1"').Count) {
    throw 'MSIX project activation must pass quoted file paths to Core.'
}
Write-Host '[OK] Project icon frames, type distinction and MSIX file activation contract.'
