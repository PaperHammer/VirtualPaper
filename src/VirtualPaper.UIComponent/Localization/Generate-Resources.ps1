#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$SourceRoot,
    [string]$MappingFile,
    [string]$XamlRoot,
    [Parameter(Mandatory)][string]$OutputRoot,
    [switch]$Check
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Xml.Linq
if (!$SourceRoot) { $SourceRoot = Join-Path $PSScriptRoot '../Strings' }
if (!$MappingFile) { $MappingFile = Join-Path $PSScriptRoot 'XamlResources.xml' }
if (!$XamlRoot) { $XamlRoot = Join-Path $PSScriptRoot '../..' }

function Read-Xml([string]$Path) {
    return [System.Xml.Linq.XDocument]::Load([IO.Path]::GetFullPath($Path), [System.Xml.Linq.LoadOptions]::PreserveWhitespace)
}
function Get-AttributeValue([System.Xml.Linq.XElement]$Element, [string]$Name) {
    $attribute = $Element.Attribute($Name)
    if ($null -eq $attribute) { return '' }
    return $attribute.Value
}

# Keep mappings independent of translations and developer comments.
$mapping = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
foreach ($entry in (Read-Xml $MappingFile).Root.Elements('Resource')) {
    $name = Get-AttributeValue $entry 'Name'
    $source = Get-AttributeValue $entry 'Source'
    if ($name -notmatch '^Ui_[^.]+\..+$' -or [string]::IsNullOrWhiteSpace($source) -or $source.StartsWith('Ui_')) {
        throw "Invalid XAML resource mapping: '$name' -> '$source'."
    }
    if ($mapping.ContainsKey($name)) { throw "Duplicate XAML resource mapping: $name" }
    $mapping.Add($name, $source)
}
if ($mapping.Count -eq 0) { throw 'XAML resource mappings are empty.' }
[string[]]$mappingNames = @($mapping.Keys)
[Array]::Sort($mappingNames, [StringComparer]::Ordinal)

# Fail at build time when a new XAML UID has no property mapping. Skip build
# outputs so an old generated/copied XAML file cannot affect a clean build.
$uids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($name in $mapping.Keys) { [void]$uids.Add($name.Substring(0, $name.IndexOf('.'))) }
$directories = [Collections.Generic.Stack[string]]::new()
$directories.Push([IO.Path]::GetFullPath($XamlRoot))
while ($directories.Count -gt 0) {
    $directory = $directories.Pop()
    foreach ($child in [IO.Directory]::EnumerateDirectories($directory)) {
        if ([IO.Path]::GetFileName($child) -notin @('bin', 'obj', 'Plugins', '.git', 'node_modules')) { $directories.Push($child) }
    }
    foreach ($path in [IO.Directory]::EnumerateFiles($directory, '*.xaml')) {
        $document = Read-Xml $path
        foreach ($attribute in $document.Descendants().Attributes()) {
            if ($attribute.Name.NamespaceName -ne 'using:WinUI3Localizer' -or $attribute.Name.LocalName -ne 'Uids.Uid') { continue }
            $uid = $attribute.Value
            if (!$uid.StartsWith('{') -and !$uids.Contains($uid)) {
                throw "XAML UID '$uid' has no mapping in XamlResources.xml ($path)."
            }
        }
    }
}

$sourceRootPath = [IO.Path]::GetFullPath($SourceRoot)
$outputRootPath = [IO.Path]::GetFullPath($OutputRoot)
if ($sourceRootPath -eq $outputRootPath -or $outputRootPath.StartsWith($sourceRootPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Generated resources must be outside the source Strings directory.'
}
$files = @([IO.Directory]::EnumerateFiles($sourceRootPath, 'Resources.resw', [IO.SearchOption]::AllDirectories) | Sort-Object)
if ($files.Count -eq 0) { throw "No source resources found in $sourceRootPath." }
foreach ($path in $files) {
    $locale = [IO.Path]::GetFileName([IO.Path]::GetDirectoryName($path))
    $document = Read-Xml $path
    $entries = [Collections.Generic.Dictionary[string,System.Xml.Linq.XElement]]::new([StringComparer]::Ordinal)
    foreach ($entry in $document.Root.Elements('data')) {
        $name = Get-AttributeValue $entry 'name'
        if ([string]::IsNullOrWhiteSpace($name) -or $entries.ContainsKey($name)) { throw "Missing/duplicate resource key '$name' in $locale." }
        if ($name.StartsWith('Ui_')) { throw "Generated key '$name' must not be maintained in source resources ($locale)." }
        $entries.Add($name, $entry)
    }
    $document.Root.AddBeforeSelf([System.Xml.Linq.XComment]::new(' Generated from source strings and Localization/XamlResources.xml. Do not edit. '))
    foreach ($name in $mappingNames) {
        $source = $mapping[$name]
        if (!$entries.ContainsKey($source) -or $null -eq $entries[$source].Element('value')) {
            throw "Missing source string '$source' for '$name' in $locale."
        }
        $alias = [System.Xml.Linq.XElement]::new([System.Xml.Linq.XName]::Get('data'))
        $alias.SetAttributeValue('name', $name)
        $alias.SetAttributeValue([System.Xml.Linq.XNamespace]::Xml + 'space', 'preserve')
        $alias.Add([System.Xml.Linq.XElement]::new($entries[$source].Element('value')))
        # Informational only. Validation reads the explicit mapping file.
        $alias.Add([System.Xml.Linq.XElement]::new([System.Xml.Linq.XName]::Get('comment'), "Source: $source"))
        $document.Root.Add([System.Xml.Linq.XText]::new("`r`n  "), $alias)
    }
    $document.Root.Add([System.Xml.Linq.XText]::new("`r`n"))
    $settings = [Xml.XmlWriterSettings]::new()
    $settings.Encoding = [Text.UTF8Encoding]::new($false)
    $settings.NewLineChars = "`r`n"
    $stream = [IO.MemoryStream]::new()
    $writer = [Xml.XmlWriter]::Create($stream, $settings)
    try { $document.Save($writer); $writer.Flush(); $generated = $settings.Encoding.GetString($stream.ToArray()) }
    finally { $writer.Dispose(); $stream.Dispose() }
    $target = Join-Path (Join-Path $outputRootPath $locale) 'Resources.resw'
    $matches = (Test-Path -LiteralPath $target) -and ([IO.File]::ReadAllText($target) -ceq $generated)
    if ($Check) {
        if (!$matches) { throw "Generated resources are missing or stale: $target" }
    }
    elseif (!$matches) {
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
        [IO.File]::WriteAllText($target, $generated, $settings.Encoding)
    }
    Write-Output "$locale resources: $($entries.Count) source strings, $($mapping.Count) generated XAML properties."
}
