#Requires -Version 7.2
# Render Store presentations from the same vector source as the application.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'BrandAssetDrawing.ps1')
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$artworkRoot = Join-Path $repository 'resources/store/artwork'
[xml]$master = [IO.File]::ReadAllText((Join-Path $repository 'resources/icons/virtualpaper.svg'))
foreach ($variant in @('transparent', 'tile')) {
    [xml]$svg = $master.CloneNode($true)
    if ($variant -eq 'tile') {
        $svg.DocumentElement.SelectSingleNode("*[local-name()='title']").InnerText = 'Virtual Paper rounded tile icon'
        $svg.DocumentElement.SelectSingleNode("*[local-name()='g']").SetAttribute('transform', 'matrix(0.8 0 0 0.8 64 64)')
        $gradient = $svg.CreateElement('linearGradient', $svg.DocumentElement.NamespaceURI)
        foreach ($pair in ([ordered]@{ id = 'tileBackground'; x1 = '0'; y1 = '0'; x2 = '0.4'; y2 = '1' }).GetEnumerator()) {
            $gradient.SetAttribute($pair.Key, $pair.Value)
        }
        foreach ($pair in @(@('0', '#4669A8'), @('1', '#6B50A0'))) {
            $stop = $svg.CreateElement('stop', $svg.DocumentElement.NamespaceURI)
            $stop.SetAttribute('offset', $pair[0]); $stop.SetAttribute('stop-color', $pair[1])
            $null = $gradient.AppendChild($stop)
        }
        $null = $svg.DocumentElement.SelectSingleNode("*[local-name()='defs']").AppendChild($gradient)
        $rect = $svg.CreateElement('rect', $svg.DocumentElement.NamespaceURI)
        foreach ($pair in ([ordered]@{ width = '640'; height = '640'; rx = '120'; ry = '120'; fill = 'url(#tileBackground)' }).GetEnumerator()) {
            $rect.SetAttribute($pair.Key, $pair.Value)
        }
        $null = $svg.DocumentElement.InsertBefore($rect, $svg.DocumentElement.SelectSingleNode("*[local-name()='g']"))
    }
    $path = Join-Path $artworkRoot "app-icon-$variant.svg"
    $settings = [Xml.XmlWriterSettings]::new()
    $settings.Indent = $true; $settings.OmitXmlDeclaration = $true
    $settings.Encoding = [Text.UTF8Encoding]::new($false)
    $writer = [Xml.XmlWriter]::Create($path, $settings)
    try { $svg.Save($writer) } finally { $writer.Dispose() }
    $drawing = Read-BrandSvg $path
    foreach ($size in @(300, 512, 1024)) {
        Export-BrandBitmap (Get-BrandBitmap $drawing 640 640 $size $size) (Join-Path $artworkRoot "app-icon-$variant-$size.png")
        Write-Host "[OK] Store $variant $size x $size"
    }
}
