$ErrorActionPreference = 'Stop'

function Invoke-ReleaseGh {
    param([Parameter(Mandatory)][string[]]$Arguments)
    $output = & gh @Arguments
    if ($LASTEXITCODE -ne 0) { throw "gh $($Arguments -join ' ') failed ($LASTEXITCODE)." }
    return $output
}

function Get-ReleaseHistory {
    param([Parameter(Mandatory)][string]$Repository)
    # --slurp preserves page boundaries; flatten explicitly, including repositories with >100 releases.
    $json = (Invoke-ReleaseGh @('api', "repos/$Repository/releases?per_page=100", '--paginate', '--slurp')) -join "`n"
    $pages = ConvertFrom-Json -InputObject $json -AsHashtable -NoEnumerate
    $releases = @(foreach ($page in $pages) {
        foreach ($release in $page) {
            if ($release.tag_name -cmatch '^v\d+\.\d+\.\d+\.\d+(plugin[A-Z]+)?$') { $release }
        }
    })
    $published = @($releases | Where-Object { -not $_.draft -and -not $_.prerelease } |
        Sort-Object { [datetime]$_.published_at } -Descending)
    $builds = @(foreach ($release in $releases) {
        # Include draft build numbers as already allocated, without using their unpublished plugin state.
        if ($release.name -match '\(Build (\d{4}R\d{2,})\)$') { $Matches[1] }
        if ($release.tag_name -cmatch 'plugin([A-Z]+)$') {
            ($Matches[1].ToCharArray() | ForEach-Object {
                if ($_ -ge 'A' -and $_ -le 'J') { [char]([int][char]'0' + [int]$_ - [int][char]'A') } else { $_ }
            }) -join ''
        }
    })
    return [pscustomobject]@{
        Previous = if ($published.Count) { $published[0] } else { $null }
        Builds = $builds
    }
}

function Get-ReleaseBaseline {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)]$History,
        [Parameter(Mandatory)][string]$ManifestPath
    )
    $previous = $History.Previous
    if (-not $previous) { return $null }
    $assets = @($previous.assets | Where-Object name -CEQ 'app_comp_manifest.json')
    if ($assets.Count -eq 0) {
        # A full release assigns fresh builds to all shipped plugins; no old plugin state is needed.
        # Get-ReleaseHistory still reserves any recorded builds. New-ComponentManifest rejects plugin-only releases without a baseline.
        Write-Warning "Release '$($previous.tag_name)' has no component manifest; plugin-only releases require a full release first."
        return $null
    }
    if ($assets.Count -ne 1) { throw 'Previous release has duplicate component manifests.' }
    Invoke-ReleaseGh @('release', 'download', $previous.tag_name, '--repo', $Repository,
        '--pattern', 'app_comp_manifest.json', '--output', $ManifestPath, '--clobber') | Out-Host
    $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json -AsHashtable
    Assert-ComponentManifest $manifest
    return $manifest
}

function Assert-ReleaseBuildNumber {
    param([Parameter(Mandatory)][string]$BuildNumber)
    # Accept historic R100+ as an input floor, but never generate one: clients compare ordinal strings.
    if ($BuildNumber -cnotmatch '^\d{2}(0[1-9]|1[0-2])R(0[1-9]|[1-9]\d+)$') {
        throw "Invalid release build number: $BuildNumber"
    }
}

function Assert-ComponentManifest {
    param([Parameter(Mandatory)][System.Collections.IDictionary]$Manifest)
    Assert-ReleaseBuildNumber $Manifest.app_build_number
    if ($Manifest.plugins -isnot [System.Collections.IDictionary]) { throw 'Component manifest must contain a plugins map.' }
    foreach ($plugin in @('UI', 'PlayerWeb', 'ScrSaver', 'ML', 'Shaders')) {
        if (-not $Manifest.plugins.Contains($plugin)) { throw "Component manifest is missing plugin $plugin." }
        Assert-ReleaseBuildNumber $Manifest.plugins[$plugin]
    }
}

function Get-NextReleaseBuildNumber {
    param([string[]]$PreviousBuilds = @(), [datetime]$Now = (Get-Date))
    $yearMonth = $Now.ToString('yyMM', [Globalization.CultureInfo]::InvariantCulture)
    $sequence = 1
    foreach ($build in $PreviousBuilds) {
        Assert-ReleaseBuildNumber $build
        $month = $build.Substring(0, 4)
        if ([string]::CompareOrdinal($month, $yearMonth) -gt 0) { throw "Build $build is ahead of the current month $yearMonth." }
        if ($month -eq $yearMonth) { $sequence = [Math]::Max($sequence, [int]$build.Substring(5) + 1) }
    }
    if ($sequence -gt 99) { throw "Monthly build limit R99 reached for $yearMonth; refusing an ordinal-incompatible R100 build." }
    return ('{0}R{1:D2}' -f $yearMonth, $sequence)
}

function New-ComponentManifest {
    param(
        [Parameter(Mandatory)][string]$BuildNumber,
        [switch]$PluginUpdate,
        [string[]]$UpdatePlugins = @(),
        [System.Collections.IDictionary]$Previous
    )
    Assert-ReleaseBuildNumber $BuildNumber
    $allPlugins = @('UI', 'PlayerWeb', 'ScrSaver', 'ML', 'Shaders')
    if ($PluginUpdate) {
        if (-not $Previous) { throw 'Plugin-only release requires the previous published component manifest. Prepare a full release first.' }
        Assert-ComponentManifest $Previous
        if (-not $UpdatePlugins.Count -or @($UpdatePlugins | Where-Object { $_ -cnotin $allPlugins }).Count) {
            throw 'Select at least one known plugin.'
        }
    }
    $plugins = [ordered]@{}
    foreach ($plugin in $allPlugins) {
        $plugins[$plugin] = if ($PluginUpdate -and $plugin -cnotin $UpdatePlugins) { $Previous.plugins[$plugin] } else { $BuildNumber }
    }
    return [ordered]@{ app_build_number = $BuildNumber; plugins = $plugins }
}

function Assert-ReleaseTagAvailable {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Tag,
        [switch]$AllowDraftRetry,
        [string]$ExpectedTitle,
        [string]$TargetSha
    )
    if ($Tag -cnotmatch '^v\d+\.\d+\.\d+\.\d+(plugin[A-Z]+)?$') { throw "Invalid release tag: $Tag" }
    $json = (Invoke-ReleaseGh @('api', "repos/$Repository/git/matching-refs/tags/$Tag")) -join "`n"
    $refs = @(ConvertFrom-Json -InputObject $json)
    if (@($refs | Where-Object ref -CEQ "refs/tags/$Tag").Count) { throw "Tag '$Tag' already exists; release blocked." }
    $json = (Invoke-ReleaseGh @('api', "repos/$Repository/releases?per_page=100", '--paginate', '--slurp')) -join "`n"
    $pages = ConvertFrom-Json -InputObject $json -AsHashtable -NoEnumerate
    $existing = @(foreach ($page in $pages) { foreach ($release in $page) { if ($release.tag_name -ceq $Tag) { $release } } })
    if ($existing.Count) {
        # A failed asset upload may leave our draft behind. Match all release identity fields before reusing it.
        if ($existing.Count -ne 1 -or -not $AllowDraftRetry -or -not $ExpectedTitle -or -not $TargetSha -or
            -not $existing[0].draft -or $existing[0].name -cne $ExpectedTitle -or $existing[0].target_commitish -cne $TargetSha) {
            throw "Release '$Tag' already exists; only the matching draft can be retried."
        }
    }
}
