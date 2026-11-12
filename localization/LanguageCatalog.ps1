# Shared by resource generation and package validation; no application or UI startup.
function Get-LanguageCatalog([string]$Path) {
    if (-not $Path) { $Path = Join-Path $PSScriptRoot 'LanguageCatalog.json' }
    $catalog = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    $codes = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $installers = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($language in $catalog.Languages) {
        if ($language.Code -notmatch '^[a-z]{2,3}(-[A-Z][a-z]{3})?(-[A-Z]{2}|-[0-9]{3})?$' -or
            [string]::IsNullOrWhiteSpace($language.Name) -or
            $language.InstallerName -notmatch '^[a-zA-Z][a-zA-Z0-9-]*$' -or
            $language.InstallerMessagesFile -notmatch '^(compiler:|Languages[\\/])[^"\r\n]+\.isl$' -or
            $language.InstallerMessagesFile.Contains('..')) { throw 'Invalid language catalog entry.' }
        $null = [Globalization.CultureInfo]::GetCultureInfo($language.Code)
        $license = $language.PSObject.Properties['InstallerLicenseFile']
        if ($license -and ($license.Value -notmatch '^[^"\r\n]+\.txt$' -or $license.Value.Contains('..'))) {
            throw 'Invalid installer license file.'
        }
        foreach ($code in @($language.Code) + @($language.Aliases)) {
            if ([string]::IsNullOrWhiteSpace($code) -or -not $codes.Add($code)) { throw "Duplicate or empty language code/alias: $code" }
        }
        if (-not $installers.Add($language.InstallerName)) { throw 'Duplicate installer language name.' }
    }
    if (@($catalog.Languages | Where-Object Code -CEQ $catalog.DefaultLanguage).Count -ne 1) {
        throw 'The default language must have exactly one canonical entry.'
    }
    return $catalog
}

function Assert-PackageLanguageDeclarations([string[]]$Languages, $Catalog) {
    # The Windows packaging SDK normalizes manifest language tags to uppercase.
    $unique = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    if ($Languages.Count -ne $Catalog.Languages.Count -or $Languages.Count -eq 0 -or
        $Languages[0] -ine $Catalog.DefaultLanguage) { throw 'MSIX language declarations differ from the supported language catalog.' }
    foreach ($code in $Languages) { [void]$unique.Add($code) }
    if (@(Compare-Object ($Languages | Sort-Object) ($Catalog.Languages.Code | Sort-Object)).Count -or $unique.Count -ne $Languages.Count) {
        throw 'MSIX language declarations differ from the supported language catalog.'
    }
}
