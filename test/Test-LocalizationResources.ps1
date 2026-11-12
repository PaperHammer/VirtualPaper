#Requires -Version 7.2
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$results = [IO.Path]::GetFullPath((Join-Path $repository 'TestResults'))
$fixture = Join-Path $results "localization-contract-$([guid]::NewGuid().ToString('N'))"
$source = Join-Path $fixture 'source'
$output = Join-Path $fixture 'generated'
$generator = Join-Path $repository 'localization/Generate-Resources.ps1'
. (Join-Path $repository 'localization/LanguageCatalog.ps1')
function Require([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Reject([scriptblock]$Action) { try { & $Action | Out-Null } catch { return }; throw 'Invalid localization input was accepted.' }
function Write-Text([string]$Path, [string]$Text) {
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}
$arguments = @{ SourceRoot = $source; CatalogPath = "$fixture/catalog.json"; ResourceMappingFile = "$fixture/resources.xml"
    MappingFile = "$fixture/xaml.xml"; XamlRoot = "$fixture/xaml"; ManifestTemplate = "$fixture/Package.appxmanifest"; OutputRoot = $output }
function Reject-ChangedFile([string]$Path, [string]$Text) {
    $original = [IO.File]::ReadAllBytes($Path)
    try { Write-Text $Path $Text; Reject { & $generator @arguments } }
    finally { [IO.File]::WriteAllBytes($Path, $original) }
}
try {
    $catalog = Get-Content "$repository/localization/LanguageCatalog.json" -Raw | ConvertFrom-Json
    # The fixture starts with the two historical languages, independent of later additions.
    $catalog.Languages = @($catalog.Languages | Where-Object Code -In @('en-US', 'zh-CN'))
    Assert-PackageLanguageDeclarations @('EN-US', 'ZH-CN') $catalog
    foreach ($declared in @(@('en-US'), @('zh-CN', 'en-US'), @('en-US', 'de-DE'), @('en-US', 'EN-US'))) {
        Reject { Assert-PackageLanguageDeclarations $declared $catalog }
    }
    Write-Text $arguments.CatalogPath ($catalog | ConvertTo-Json -Depth 6)
    $texts = '<root><data name="Greeting"><value>Ready {0}</value></data><data name="CodeOnly"><value>Code-only message</value></data><data name="Installer"><value>Continue?</value></data></root>'
    foreach ($code in @('en-US', 'zh-CN')) { Write-Text "$source/$code/Resources.resw" $texts }
    Write-Text $arguments.ResourceMappingFile '<Resources><Resource Target="Core" Name="Title" Source="Greeting"/><Resource Target="Common" Name="Message" Source="Greeting"/><Resource Target="Installer" Name="Continue" Source="Installer"/></Resources>'
    Write-Text $arguments.MappingFile '<Resources><Resource Name="Ui_Greeting.Text" Source="Greeting"/></Resources>'
    Write-Text "$fixture/xaml/Page.xaml" '<Page xmlns="urn:test" xmlns:l="using:WinUI3Localizer"><TextBlock l:Uids.Uid="Ui_Greeting"/></Page>'
    Write-Text $arguments.ManifestTemplate '<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"><Resources/></Package>'
    & $generator @arguments | Out-Null
    & $generator @arguments -Check | Out-Null
    & $generator @arguments -Target Metadata -OutputRoot "$fixture/metadata" | Out-Null
    [xml]$metadata = Get-Content "$fixture/metadata/Languages.props" -Raw
    Require ($metadata.Project.PropertyGroup.DefaultLanguage -ceq 'en-US') 'Native WinUI heads must share the catalog default.'
    $timestamp = [IO.File]::GetLastWriteTimeUtc("$output/UI/en-US/Resources.resw")
    & $generator @arguments | Out-Null
    Require ([IO.File]::GetLastWriteTimeUtc("$output/UI/en-US/Resources.resw") -eq $timestamp) 'Unchanged generated resources must retain their timestamps.'
    [xml]$ui = Get-Content "$output/UI/en-US/Resources.resw" -Raw
    Require (($ui.root.data | Where-Object name -EQ 'Ui_Greeting.Text').value -ceq 'Ready {0}') 'Generated XAML alias lost its source value.'
    Require (Test-Path "$output/Core/lang.resx") 'The default language must be the neutral Core resource.'
    Require (Test-Path "$output/Core/lang.zh-CN.resx") 'Chinese must remain an explicit Core translation.'
    Require (Test-Path "$output/Common/FileAccess.resx") 'The common neutral resource is missing.'
    $installer = Get-Content "$output/Installer/Localization.iss" -Raw
    Require ($installer.Contains('english.InitialApplicationLanguage=en-US') -and $installer.Contains('chinese.InitialApplicationLanguage=zh-CN')) 'Installer names must map to canonical language markers.'
    Require ($installer.Contains('Name: "english"; MessagesFile: "compiler:Default.isl"; LicenseFile: "License\LICENSE.txt";')) 'The existing English license page must be preserved.'
    [xml]$manifest = Get-Content "$output/Manifest/Package.appxmanifest" -Raw
    Require (($manifest.Package.Resources.Resource.Language -join ',') -ceq 'en-US,zh-CN') 'The manifest must declare the supported languages, default first.'

    $chinese = "$source/zh-CN/Resources.resw"
    Reject-ChangedFile $chinese ($texts.Replace('<data name="CodeOnly"><value>Code-only message</value></data>', ''))
    Reject-ChangedFile $chinese ($texts.Replace('CodeOnly', 'UnexpectedKey'))
    Reject-ChangedFile $chinese ($texts.Replace('{0}', '{1}'))
    Reject-ChangedFile $chinese ($texts.Replace('Code-only message', ''))
    Reject-ChangedFile $chinese ($texts.Replace('</root>', '<data name="Greeting"><value>Duplicate</value></data></root>'))
    Reject-ChangedFile $chinese ($texts.Replace('CodeOnly', 'Ui_ManuallyGenerated.Text'))
    Reject-ChangedFile $arguments.MappingFile '<Resources><Resource Name="Ui_Greeting.Text" Source="Missing"/></Resources>'
    Reject-ChangedFile $arguments.MappingFile '<Resources><Resource Name="Ui_Greeting.Text" Source="Greeting"/><Resource Name="Ui_Greeting.Text" Source="Greeting"/></Resources>'
    Reject-ChangedFile "$fixture/xaml/Page.xaml" '<Page xmlns="urn:test" xmlns:l="using:WinUI3Localizer"><TextBlock l:Uids.Uid="Ui_Missing"/></Page>'
    $runtimeMap = Get-Content $arguments.ResourceMappingFile -Raw
    Reject-ChangedFile $arguments.ResourceMappingFile ($runtimeMap.Replace('</Resources>', '<Resource Target="Core" Name="Title" Source="Greeting"/></Resources>'))
    Reject-ChangedFile $arguments.ResourceMappingFile ($runtimeMap.Replace('Target="Common"', 'Target="Unknown"'))
    $catalog.Languages[1].Aliases += 'zh'
    Reject-ChangedFile $arguments.CatalogPath ($catalog | ConvertTo-Json -Depth 6)
    $catalog = Get-Content $arguments.CatalogPath -Raw | ConvertFrom-Json
    ($catalog.Languages | Where-Object Code -EQ 'en-US').InstallerLicenseFile = "License`"; Unexpected: value"
    Reject-ChangedFile $arguments.CatalogPath ($catalog | ConvertTo-Json -Depth 6)
    $catalog = Get-Content $arguments.CatalogPath -Raw | ConvertFrom-Json
    $catalog.DefaultLanguage = 'missing'
    Reject-ChangedFile $arguments.CatalogPath ($catalog | ConvertTo-Json -Depth 6)
    Reject { & $generator @arguments -OutputRoot $source }
    Reject { & $generator @arguments -OutputRoot $fixture }

    $generatedPath = "$output/UI/en-US/Resources.resw"
    # -Check diagnoses stale content without repairing it.
    $original = [IO.File]::ReadAllBytes($generatedPath)
    try { Write-Text $generatedPath '<root/>'; Reject { & $generator @arguments -Check }; Require ([IO.File]::ReadAllText($generatedPath) -ceq '<root/>') 'Check mode modified output.' }
    finally { [IO.File]::WriteAllBytes($generatedPath, $original) }
    $retired = "$output/UI/retired/Resources.resw"
    Write-Text $retired ([IO.File]::ReadAllText($generatedPath))
    Write-Text "$output/UI/notes.txt" 'unrelated output'
    Reject { & $generator @arguments -Check }
    & $generator @arguments | Out-Null
    Require (-not (Test-Path $retired) -and [IO.File]::ReadAllText("$output/UI/notes.txt") -ceq 'unrelated output') 'Retired-resource cleanup changed unrelated files.'

    # A third catalog entry must propagate through every consumer without code changes.
    $catalog = Get-Content $arguments.CatalogPath -Raw | ConvertFrom-Json
    $catalog.Languages += [pscustomobject]@{ Code = 'de-DE'; Name = 'Deutsch'; Aliases = @('de'); InstallerName = 'german'; InstallerMessagesFile = 'compiler:Languages\German.isl' }
    Write-Text $arguments.CatalogPath ($catalog | ConvertTo-Json -Depth 6)
    Write-Text "$source/de-DE/Resources.resw" $texts
    & $generator @arguments | Out-Null
    & $generator @arguments -Check | Out-Null
    Require ((Test-Path "$output/UI/de-DE/Resources.resw") -and (Test-Path "$output/Core/lang.de-DE.resx") -and (Test-Path "$output/Common/FileAccess.de-DE.resx")) 'A new catalog language was not generated for all runtimes.'
    Require ((Get-Content "$output/Installer/Localization.iss" -Raw).Contains('german.InitialApplicationLanguage=de-DE')) 'A new installer language was not generated.'
    [xml]$manifest = Get-Content "$output/Manifest/Package.appxmanifest" -Raw
    Require ($manifest.Package.Resources.Resource.Language -contains 'de-DE') 'A new language is missing from the manifest.'
    Require (($manifest.Package.Resources.Resource.Language -join ',') -ceq 'en-US,zh-CN,de-DE') 'Keep non-default locales in catalog order when adding languages.'
} finally {
    $resolved = [IO.Path]::GetFullPath($fixture)
    if (-not $resolved.StartsWith($results + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notmatch '^localization-contract-[0-9a-f]{32}$') { throw 'Unexpected localization fixture cleanup path.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
Write-Host '[OK] Localization catalogs, complete keys/placeholders, all generated consumers, stale checks and source protection.'
