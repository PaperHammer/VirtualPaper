#Requires -Version 7.2
param([string]$PayloadDirectory)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/LocalizationResourceSupport.ps1"
$expected = Get-ExpectedLocalization
function Reject([scriptblock]$Action) { try { & $Action | Out-Null } catch { return }; throw 'Invalid package localization was accepted.' }
$sample = $expected.Locales['ru-RU'].UI
$copy = [Collections.Generic.Dictionary[string,string]]::new($sample, [StringComparer]::Ordinal)
Assert-LocalizedStrings $copy $sample 'fixture'
[void]$copy.Remove('SidebarIntelligentFullName')
Reject { Assert-LocalizedStrings $copy $sample 'missing source' }
$copy['SidebarIntelligentFullName'] = 'old'
Reject { Assert-LocalizedStrings $copy $sample 'stale source' }
$copy['SidebarIntelligentFullName'] = $sample['SidebarIntelligentFullName']
$alias = 'Ui_SidebarRepository_Text.Text'
$copy[$alias] = 'old tooltip'
Reject { Assert-LocalizedStrings $copy $sample 'stale mapped property' }
$copy[$alias] = $sample[$alias]
$copy['Ui_Retired.Text'] = 'retired'
Reject { Assert-LocalizedStrings $copy $sample 'retired property' }
foreach ($text in @('<root><data name="Duplicate"><value>A</value></data><data name="Duplicate"><value>B</value></data></root>',
    '<root><data name="Empty"/></root>')) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($text)
    $stream = [IO.MemoryStream]::new($bytes)
    try { Reject { Read-LocalizedUiStrings $stream } } finally { $stream.Dispose() }
}

# Match MakePri's detailed dump, including uppercase language qualifiers.
[xml]$shell = '<PriInfo><ResourceMap><ResourceMapSubtree name="Resources"/></ResourceMap></PriInfo>'
$resources = $shell.SelectSingleNode('//ResourceMapSubtree')
foreach ($key in @('Shell_OpenWithVirtualPaper', 'Shell_DesignFileType', 'Shell_WebFileType')) {
    $resource = $shell.CreateElement('NamedResource')
    $resource.SetAttribute('uri', "ms-resource://VirtualPaper.Test/Resources/$key")
    foreach ($language in $expected.Catalog.Languages) {
        $candidate = $shell.CreateElement('Candidate'); $candidate.SetAttribute('type', 'String')
        $set = $shell.CreateElement('QualifierSet')
        $qualifier = $shell.CreateElement('Qualifier'); $qualifier.SetAttribute('name', 'Language')
        $qualifier.SetAttribute('value', $language.Code.ToUpperInvariant())
        [void]$set.AppendChild($qualifier); [void]$candidate.AppendChild($set)
        $value = $shell.CreateElement('Value'); $value.InnerText = $expected.Locales[$language.Code].UI[$key]
        [void]$candidate.AppendChild($value); [void]$resource.AppendChild($candidate)
    }
    [void]$resources.AppendChild($resource)
}
Assert-PackageShellResources $shell 'VirtualPaper.Test' $expected
foreach ($mutate in @(
    { param($xml) $node = $xml.SelectSingleNode('//NamedResource'); [void]$node.ParentNode.RemoveChild($node) },
    { param($xml) $node = $xml.SelectSingleNode('//Candidate'); [void]$node.ParentNode.RemoveChild($node) },
    { param($xml) $xml.SelectSingleNode('//NamedResource').SetAttribute('uri', 'ms-resource://Wrong.Package/Resources/Shell_OpenWithVirtualPaper') },
    { param($xml) $xml.SelectSingleNode('//Candidate').SetAttribute('type', 'Path') },
    { param($xml) $xml.SelectSingleNode('//Candidate/Value').InnerText = 'stale shell label' },
    { param($xml) $xml.SelectSingleNode('//Candidate/QualifierSet/Qualifier').SetAttribute('value', 'missing') },
    {
        param($xml)
        # Every expected text still exists, but English and Chinese resolve incorrectly.
        $resource = $xml.SelectSingleNode('//NamedResource')
        $english = $resource.SelectSingleNode("Candidate[QualifierSet/Qualifier[@value='EN-US']]/Value")
        $chinese = $resource.SelectSingleNode("Candidate[QualifierSet/Qualifier[@value='ZH-CN']]/Value")
        $english.InnerText, $chinese.InnerText = $chinese.InnerText, $english.InnerText
    },
    {
        param($xml)
        $resource = $xml.SelectSingleNode('//NamedResource')
        $resource.SelectSingleNode("Candidate/QualifierSet/Qualifier[@value='ZH-CN']").SetAttribute('value', 'EN-US')
    }
)) {
    [xml]$changed = $shell.OuterXml
    & $mutate $changed
    Reject { Assert-PackageShellResources $changed 'VirtualPaper.Test' $expected }
}
# Exercise the complete archive/directory path traversal without a build dependency.
# This scoped mock supplies assembly contents only; real payload runs below read actual DLL resources.
$actualReader = ${function:Read-LocalizedAssemblyStrings}
try {
    function Read-LocalizedAssemblyStrings([IO.Stream]$Stream, [string]$ResourceName) {
        $reader = [IO.StreamReader]::new($Stream, [Text.Encoding]::UTF8, $false, 1024, $true)
        try { $path = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $locale = ($expected.Catalog.Languages.Code | Where-Object { $path -match "(^|/)$([regex]::Escape($_))/" } | Select-Object -First 1)
        if (-not $locale) { $locale = $expected.Catalog.DefaultLanguage }
        $kind = if ($path -like '*VirtualPaper.Common*') { 'Common' } else { 'Core' }
        return ,$expected.Locales[$locale][$kind]
    }
    $missing = ''; $requested = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $open = {
        param($path)
        [void]$requested.Add($path)
        if ($path -ceq $missing) { throw "Missing fixture file: $path" }
        if ($path.EndsWith('.resw')) {
            $locale = $path.Split('/')[-2]
            $doc = [Xml.Linq.XDocument]::Parse('<root/>')
            foreach ($key in $expected.Locales[$locale].UI.Keys) {
                $entry = [Xml.Linq.XElement]::new([Xml.Linq.XName]::Get('data'))
                $entry.SetAttributeValue('name', $key)
                $entry.Add([Xml.Linq.XElement]::new([Xml.Linq.XName]::Get('value'), $expected.Locales[$locale].UI[$key]))
                $doc.Root.Add($entry)
            }
            return [IO.MemoryStream]::new([Text.Encoding]::UTF8.GetBytes($doc.ToString()))
        }
        return [IO.MemoryStream]::new([Text.Encoding]::UTF8.GetBytes($path))
    }
    Assert-PackageLocalization $open $expected
    if ($requested.Count -ne $expected.Catalog.Languages.Count * 4) { throw 'Not every UI/Core/Common location was checked.' }
    foreach ($missingPath in @('Plugins/UI/VirtualPaper.UIComponent/Strings/fr-FR/Resources.resw',
        'ru-RU/VirtualPaper.resources.dll', 'fr-FR/VirtualPaper.Common.resources.dll', 'Plugins/UI/zh-TW/VirtualPaper.Common.resources.dll')) {
        $missing = $missingPath
        Reject { Assert-PackageLocalization $open $expected }
    }
} finally { ${function:Read-LocalizedAssemblyStrings} = $actualReader }
if ($PayloadDirectory) {
    $root = (Resolve-Path -LiteralPath $PayloadDirectory).Path
    Assert-PackageLocalization { param($path) [IO.File]::OpenRead((Join-Path $root $path)) } $expected
    # Exercise the final archive reader with real build bytes, including a stale
    # translation whose key count and XML remain valid. No application is installed.
    foreach ($corrupt in @($false, $true)) {
        $zipPath = Join-Path ([IO.Path]::GetTempPath()) "VP.Localization.$([guid]::NewGuid().ToString('N')).zip"
        try {
            $archive = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
            try {
                foreach ($path in $requested) {
                    $entry = $archive.CreateEntry($path)
                    $target = $entry.Open()
                    try {
                        if ($corrupt -and $path -ceq 'Plugins/UI/VirtualPaper.UIComponent/Strings/fr-FR/Resources.resw') {
                            $doc = [Xml.Linq.XDocument]::Load((Join-Path $root $path))
                            ($doc.Root.Elements('data') | Where-Object { $_.Attribute('name').Value -ceq 'SidebarIntelligentFullName' }).Element('value').Value = 'stale'
                            $writer = [IO.StreamWriter]::new($target, [Text.UTF8Encoding]::new($false), 1024, $true)
                            try { $writer.Write($doc.ToString()) } finally { $writer.Dispose() }
                        } else {
                            $input = [IO.File]::OpenRead((Join-Path $root $path))
                            try { $input.CopyTo($target) } finally { $input.Dispose() }
                        }
                    } finally { $target.Dispose() }
                }
            } finally { $archive.Dispose() }
            $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
            try {
                $openArchive = { param($path) $entry = $archive.GetEntry($path); if ($null -eq $entry) { throw "Missing archive entry: $path" }; $entry.Open() }
                if ($corrupt) { Reject { Assert-PackageLocalization $openArchive $expected } }
                else { Assert-PackageLocalization $openArchive $expected }
            } finally { $archive.Dispose() }
        } finally { if (Test-Path -LiteralPath $zipPath) { [IO.File]::Delete($zipPath) } }
    }
    Write-Host '[OK] Actual Release UI/Core/Common language resources match source translations.'
}
Write-Host '[OK] Localization payload checks reject missing, stale, duplicate and retired resources.'
