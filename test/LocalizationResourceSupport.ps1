# Shared checks for the Release payload, installed EXE and final MSIX archive.
. (Join-Path $PSScriptRoot '../localization/LanguageCatalog.ps1')

function Get-ExpectedLocalization {
    $root = Join-Path $PSScriptRoot '../localization'
    $catalog = Get-LanguageCatalog
    [xml]$mappings = Get-Content (Join-Path $root 'ResourceMappings.xml') -Raw -Encoding utf8
    [xml]$xaml = Get-Content (Join-Path $root '../src/VirtualPaper.UIComponent/Localization/XamlResources.xml') -Raw -Encoding utf8
    $locales = @{}
    foreach ($language in $catalog.Languages) {
        [xml]$source = Get-Content (Join-Path $root "Strings/$($language.Code)/Resources.resw") -Raw -Encoding utf8
        $values = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
        foreach ($entry in $source.root.data) { $values.Add([string]$entry.name, [string]$entry.value) }
        $ui = [Collections.Generic.Dictionary[string,string]]::new($values, [StringComparer]::Ordinal)
        foreach ($entry in $xaml.Resources.Resource) { $ui.Add([string]$entry.Name, $values[[string]$entry.Source]) }
        $runtime = @{}
        foreach ($kind in @('Core', 'Common')) {
            $runtime[$kind] = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
            foreach ($entry in $mappings.Resources.Resource | Where-Object Target -CEQ $kind) {
                $runtime[$kind].Add([string]$entry.Name, $values[[string]$entry.Source])
            }
        }
        $locales[$language.Code] = @{ UI = $ui; Core = $runtime.Core; Common = $runtime.Common }
    }
    return @{ Catalog = $catalog; Locales = $locales }
}

function Assert-LocalizedStrings($Actual, $Expected, [string]$Description) {
    if ($Actual.Count -ne $Expected.Count) { throw "Localization key count differs: $Description" }
    foreach ($key in $Expected.Keys) {
        if (-not $Actual.ContainsKey($key) -or
            $Actual[$key].Replace("`r`n", "`n") -cne $Expected[$key].Replace("`r`n", "`n")) {
            throw "Missing or stale localization value: $Description/$key"
        }
    }
}

function Read-LocalizedUiStrings([IO.Stream]$Stream) {
    $xml = [Xml.Linq.XDocument]::Load($Stream)
    $values = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    foreach ($entry in $xml.Root.Elements('data')) {
        $attribute = $entry.Attribute('name')
        $key = if ($null -ne $attribute) { $attribute.Value } else { '' }
        if (-not $key -or $null -eq $entry.Element('value')) { throw 'Malformed packaged UI translation.' }
        $values.Add($key, $entry.Element('value').Value)
    }
    return ,$values
}

function Read-LocalizedAssemblyStrings([IO.Stream]$Stream, [string]$ResourceName) {
    $context = [Runtime.Loader.AssemblyLoadContext]::new("Localization-$([guid]::NewGuid())", $true)
    $image = [IO.MemoryStream]::new()
    try {
        # ZIP entry streams are not seekable; the loader needs a seekable image.
        $Stream.CopyTo($image)
        $image.Position = 0
        $assembly = $context.LoadFromStream($image)
        $resource = $assembly.GetManifestResourceStream($ResourceName)
        if ($null -eq $resource) { throw "Missing packaged assembly resource: $ResourceName" }
        $reader = [Resources.ResourceReader]::new($resource)
        try {
            $values = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
            $iterator = $reader.GetEnumerator()
            while ($iterator.MoveNext()) { $values.Add([string]$iterator.Key, [string]$iterator.Value) }
            return ,$values
        } finally { $reader.Dispose() }
    } finally { $image.Dispose(); $context.Unload() }
}

function Assert-PackageLocalization([scriptblock]$OpenFile, $Expected = (Get-ExpectedLocalization)) {
    foreach ($language in $Expected.Catalog.Languages) {
        $locale = $language.Code
        $uiPath = "Plugins/UI/VirtualPaper.UIComponent/Strings/$locale/Resources.resw"
        $stream = & $OpenFile $uiPath
        try { Assert-LocalizedStrings (Read-LocalizedUiStrings $stream) $Expected.Locales[$locale].UI $uiPath }
        finally { $stream.Dispose() }
        $suffix = if ($locale -ceq $Expected.Catalog.DefaultLanguage) { '' } else { ".$locale" }
        foreach ($owner in @(
            @{ Kind = 'Core'; Assembly = 'VirtualPaper'; Directory = ''; Resource = 'VirtualPaper.Properties.lang' },
            @{ Kind = 'Common'; Assembly = 'VirtualPaper.Common'; Directory = ''; Resource = 'VirtualPaper.Common.Resources.FileAccess' },
            @{ Kind = 'Common'; Assembly = 'VirtualPaper.Common'; Directory = 'Plugins/UI/'; Resource = 'VirtualPaper.Common.Resources.FileAccess' }
        )) {
            $path = if ($suffix) { "$($owner.Directory)$locale/$($owner.Assembly).resources.dll" }
                else { "$($owner.Directory)$($owner.Assembly).dll" }
            $stream = & $OpenFile $path
            try {
                $values = Read-LocalizedAssemblyStrings $stream "$($owner.Resource)$suffix.resources"
                Assert-LocalizedStrings $values $Expected.Locales[$locale][$owner.Kind] $path
            } finally { $stream.Dispose() }
        }
    }
}
