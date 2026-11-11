# MakePri's PRIINFO indexer preserves embedded XBF/string data while file
# candidates are relocated into Plugins/UI. Existing XAML URI names stay intact.
function Convert-MsixUIResources {
    param(
        [Parameter(Mandatory)][string] $UiDirectory,
        [Parameter(Mandatory)][string] $UiBuildDirectory,
        [Parameter(Mandatory)][string] $UiSourceDirectory,
        [Parameter(Mandatory)][string] $PayloadDirectory,
        [Parameter(Mandatory)][string] $WorkDirectory,
        [Parameter(Mandatory)][string] $MakePri
    )
    [void](New-Item -ItemType Directory -Path $WorkDirectory -Force)
    # Build-only Content targets (notably generated translations) are not run
    # for references when publishing with BuildProjectReferences=false.
    foreach ($file in Get-ChildItem -LiteralPath $UiBuildDirectory -Recurse -File |
        Where-Object { $_.Extension -notin @('.pri', '.pdb') }) {
        $destination = Join-Path $UiDirectory ([IO.Path]::GetRelativePath($UiBuildDirectory, $file.FullName))
        if (-not (Test-Path -LiteralPath $destination)) {
            [void](New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force)
            Copy-Item -LiteralPath $file.FullName -Destination $destination
        }
    }
    foreach ($pri in Get-ChildItem -LiteralPath $UiDirectory -Filter '*.pri' -File) {
        $dump = Join-Path $WorkDirectory "$($pri.Name).xml"
        & $MakePri dump /if $pri.FullName /of $dump /dt detailed /o | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Cannot dump WinUI resource index: $($pri.Name)" }
        [xml]$info = Get-Content -LiteralPath $dump -Raw
        foreach ($value in $info.SelectNodes('//Candidate[@type="Path"]/Value')) {
            $relativePath = $value.InnerText.Replace('\', '/')
            $source = [IO.Path]::GetFullPath((Join-Path $UiDirectory $relativePath))
            $uiRoot = [IO.Path]::GetFullPath($UiDirectory).TrimEnd('\') + '\'
            if (-not $source.StartsWith($uiRoot, [StringComparison]::OrdinalIgnoreCase)) {
                throw "WinUI PRI references an outside file: $relativePath"
            }
            # WinUI PRI also lists source Assets without CopyToPublish metadata.
            if (-not (Test-Path -LiteralPath $source -PathType Leaf) -and $relativePath.StartsWith('Assets/')) {
                $assetSource = Join-Path $UiSourceDirectory $relativePath
                if (Test-Path -LiteralPath $assetSource -PathType Leaf) {
                    [void](New-Item -ItemType Directory -Path (Split-Path -Parent $source) -Force)
                    Copy-Item -LiteralPath $assetSource -Destination $source
                }
            }
            if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
                throw "WinUI PRI references a missing/outside file: $relativePath"
            }
            $value.InnerText = "Plugins\UI\$($relativePath.Replace('/', '\'))"
        }
        $info.Save($dump)
        $config = Join-Path $WorkDirectory "$($pri.Name).config.xml"
        [xml]$configuration = '<resources targetOsVersion="10.0.0" majorVersion="1"><index><default><qualifier name="Language" value="en-US" /></default><indexer-config type="PRIINFO" emitStrings="true" emitPaths="true" emitEmbeddedData="true" /></index></resources>'
        $configuration.resources.index.SetAttribute('root', $WorkDirectory)
        $configuration.resources.index.SetAttribute('startIndexAt', [IO.Path]::GetFileName($dump))
        $configuration.Save($config)
        $indexName = @($info.PriInfo.ResourceMap)[0].name
        $targetName = if ($pri.Name -ieq 'resources.pri') { 'VirtualPaper.UI.Resources.pri' } else { $pri.Name }
        & $MakePri new /pr $PayloadDirectory /cf $config /in $indexName /of (Join-Path $PayloadDirectory $targetName) /o | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Cannot relocate WinUI resource index: $($pri.Name)" }
    }
    $assets = Join-Path $PayloadDirectory 'MsixAssets'
    [void](New-Item -ItemType Directory -Path $assets -Force)
    foreach ($name in @('StoreLogo.scale-200.png', 'Square150x150Logo.scale-200.png', 'Square44x44Logo.scale-200.png')) {
        Copy-Item -LiteralPath (Join-Path $UiDirectory "Assets/$name") -Destination $assets
    }
}

function Assert-VirtualPaperWpfResources([IO.Stream] $AssemblyStream, [string] $ProductVersion) {
    $loadContext = [Runtime.Loader.AssemblyLoadContext]::new("MSIX-WPF-$([guid]::NewGuid())", $true)
    $assemblyBytes = [IO.MemoryStream]::new()
    $entryStream = $AssemblyStream
    try {
        $entryStream.CopyTo($assemblyBytes); $assemblyBytes.Position = 0
        $assembly = $loadContext.LoadFromStream($assemblyBytes)
        if ($ProductVersion -and $assembly.GetName().Version -ne [version]$ProductVersion) {
            throw 'Packaged Core version differs from the shared Release build.'
        }
        $resourceStream = $assembly.GetManifestResourceStream('VirtualPaper.g.resources')
        if (-not $resourceStream) { throw 'MSIX Core is missing WPF resources.' }
        $resourceReader = [Resources.ResourceReader]::new($resourceStream)
        try {
            $keys = @(); $enumerator = $resourceReader.GetEnumerator()
            while ($enumerator.MoveNext()) { $keys += [string]$enumerator.Key }
            foreach ($key in @('app.baml', 'mainwindow.baml', 'views/windowsmsg/rawinputmsgwindow.baml', 'views/windowsmsg/wndprocmsgwindow.baml')) {
                if ($keys -cnotcontains $key) { throw "MSIX Core lost compiled WPF XAML: $key" }
            }
        } finally { $resourceReader.Dispose() }
    } finally { $assemblyBytes.Dispose(); $loadContext.Unload() }
}
