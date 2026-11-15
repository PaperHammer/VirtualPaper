#Requires -Version 7.2
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$root = Join-Path $repository 'doc/legal'
$catalog = Get-Content (Join-Path $repository 'localization/LanguageCatalog.json') -Raw | ConvertFrom-Json
$utf8 = [Text.UTF8Encoding]::new($false, $true)
$sections = @('privacy-policy', 'ai-processing-and-rights', 'copyright-and-licenses')
$keys = @('Settings_Others_Legal_Privacy', 'Settings_Others_Legal_Ai', 'Settings_Others_Legal_Copyright')
function Require([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
$english = [IO.File]::ReadAllText((Join-Path $root "$($catalog.DefaultLanguage)/README.md"), $utf8)
$date = [regex]::Match($english, '\d{4}-\d{2}-\d{2}').Value
Require ($date.Length -eq 10) 'The policy update date is missing.'
foreach ($language in $catalog.Languages) {
    $code = $language.Code
    $path = Join-Path $root "$code/README.md"
    $text = [IO.File]::ReadAllText($path, $utf8)
    Require ($text.Contains($date) -and $text.Contains('1.0')) "Policy metadata is inconsistent: $code"
    Require ($code -eq $catalog.DefaultLanguage -or $text -cne $english) "Policy translation is an English copy: $code"
    $previous = -1
    foreach ($section in $sections) {
        $marker = '<a id="' + $section + '"></a>'
        $position = $text.IndexOf($marker, [StringComparison]::Ordinal)
        Require ($position -gt $previous -and $position -eq $text.LastIndexOf($marker, [StringComparison]::Ordinal)) "Missing, duplicate or unordered policy section $section in $code"
        $previous = $position
    }
    foreach ($match in [regex]::Matches($text, '\[[^\]]+\]\(([^)\s]+)\)')) {
        $target = $match.Groups[1].Value
        if ($target.StartsWith('https://', [StringComparison]::Ordinal)) { continue }
        $resolved = [IO.Path]::GetFullPath($target, [IO.Path]::GetDirectoryName($path))
        Require ($resolved.StartsWith(([IO.Path]::GetFullPath($root) + [IO.Path]::DirectorySeparatorChar), [StringComparison]::OrdinalIgnoreCase)) "Legal link escapes the bundle: $target"
        Require (Test-Path -LiteralPath $resolved -PathType Leaf) "Missing offline legal link: $target"
    }
    [xml]$strings = Get-Content (Join-Path $repository "localization/Strings/$code/Resources.resw") -Raw
    foreach ($key in $keys) {
        $entry = @($strings.root.data | Where-Object name -CEQ $key)
        Require ($entry.Count -eq 1 -and -not [string]::IsNullOrWhiteSpace($entry[0].value)) "Missing legal UI label $key in $code"
    }
}
foreach ($copy in @('doc/legal/licenses/GPL-3.0.txt', 'packaging/inno/License/LICENSE.txt')) {
    Require ((Get-FileHash (Join-Path $repository $copy)).Hash -ceq (Get-FileHash (Join-Path $repository 'LICENSE')).Hash) "Application license differs from repository GPLv3: $copy"
}
foreach ($wallpaper in Get-ChildItem (Join-Path $repository 'resources/BuiltInWallpapers') -Directory) {
    foreach ($name in @('NOTICE.txt', 'CC0-1.0.txt')) {
        $source = Join-Path $wallpaper.FullName $name
        if (Test-Path -LiteralPath $source) {
            $copy = Join-Path $root "licenses/wallpapers/$($wallpaper.Name)/$name"
            Require ((Get-FileHash $source).Hash -ceq (Get-FileHash $copy).Hash) "Wallpaper notice changed or is missing: $copy"
        }
    }
}
Write-Host "[OK] Offline legal documents: $($catalog.Languages.Count) translations, section IDs, local links, UI labels and original GPL/media notices passed."
