$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../.github/scripts/ReleaseMetadata.ps1')

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -cne $Expected) { throw "$Message`: expected [$Expected], got [$Actual]." }
}
function Assert-Throws([scriptblock]$Action, [string]$Pattern) {
    try { & $Action | Out-Null } catch {
        if ($_.Exception.Message -notmatch $Pattern) { throw "Unexpected error: $($_.Exception.Message); expected $Pattern" }
        return
    }
    throw "Expected failure matching: $Pattern"
}

$november = [datetime]'2026-11-03'
Assert-Equal (Get-NextReleaseBuildNumber -Now $november) '2611R01' 'First release'
Assert-Equal (Get-NextReleaseBuildNumber @('2611R01') $november) '2611R02' 'Full release followed by plugin release'
Assert-Equal (Get-NextReleaseBuildNumber @('2611R01', '2611R07', '2611R03') $november) '2611R08' 'Highest allocated build wins'
Assert-Equal (Get-NextReleaseBuildNumber @('2610R99') $november) '2611R01' 'Month rollover'
Assert-Equal (Get-NextReleaseBuildNumber @('2610R100') $november) '2611R01' 'Historic three-digit sequence in older month'
$next = Get-NextReleaseBuildNumber @('2611R98') $november
if ([string]::CompareOrdinal($next, '2611R98') -le 0) { throw 'Generated build is not ordinal-monotonic.' }
Assert-Throws { Get-NextReleaseBuildNumber @('2611R99') $november } 'Monthly build limit'
Assert-Throws { Get-NextReleaseBuildNumber @('2611R100') $november } 'Monthly build limit'
Assert-Throws { Get-NextReleaseBuildNumber @('2612R01') $november } 'ahead of the current month'
Assert-Throws { Get-NextReleaseBuildNumber @('2613R01') $november } 'Invalid release build'

$old = New-ComponentManifest -BuildNumber '2611R01'
$old.plugins.ML = '2610R09'
$partial = New-ComponentManifest '2611R02' -PluginUpdate -UpdatePlugins UI -Previous $old
Assert-Equal $partial.plugins.UI '2611R02' 'Selected plugin updated'
Assert-Equal $partial.plugins.PlayerWeb '2611R01' 'Unselected plugin preserved'
Assert-Equal $partial.plugins.ML '2610R09' 'Older unselected plugin preserved'
$full = New-ComponentManifest '2611R03' -Previous $partial
foreach ($value in $full.plugins.Values) { Assert-Equal $value '2611R03' 'Full release marks every shipped plugin' }
Assert-Throws { New-ComponentManifest '2611R04' -PluginUpdate -UpdatePlugins UI } 'requires the previous published'
Assert-Throws { New-ComponentManifest '2611R04' -PluginUpdate -Previous $old } 'Select at least one known plugin'
Assert-Throws { New-ComponentManifest '2611R04' -PluginUpdate -UpdatePlugins Unknown -Previous $old } 'Select at least one known plugin'
Assert-Throws { Assert-ComponentManifest @{ app_build_number = '2611R01'; plugins = @{ UI = '2611R01' } } } 'missing plugin'

$fixture = Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../TestResults'))) "release-metadata-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$script:downloadPath = Join-Path $fixture 'previous.json'
$script:downloadMode = 'success'
$script:apiFails = $false
$script:refs = @()
$script:calls = [Collections.Generic.List[object]]::new()
$script:published = @{
    tag_name = 'v0.5.3.0'; name = 'v0.5.3.0 (Build 2611R01)'; draft = $false; prerelease = $false
    published_at = '2026-11-02T00:00:00Z'; target_commitish = 'published-sha'
    assets = @(@{ name = 'app_comp_manifest.json' })
}
$script:draft = @{
    tag_name = 'v0.5.3.0pluginCGBBRAF'; name = 'v0.5.3.0 Plugin Update (Build 2611R05)'
    draft = $true; prerelease = $false; target_commitish = 'draft-sha'; assets = @()
}
$script:releasePages = @(@($script:draft), @($script:published))

# Override only gh in this script scope: no GitHub calls, authentication, releases or refs are modified.
function gh {
    $script:calls.Add(@($args))
    $global:LASTEXITCODE = 0
    if ($script:apiFails) { $global:LASTEXITCODE = 4; return }
    if ($args[0] -eq 'api') {
        if ($args[1] -like '*/git/matching-refs/*') { return ConvertTo-Json -InputObject $script:refs -Depth 10 -Compress }
        return ConvertTo-Json -InputObject $script:releasePages -Depth 10 -Compress
    }
    if ($args[0] -eq 'release' -and $args[1] -eq 'download') {
        if ($script:downloadMode -eq 'failure') { $global:LASTEXITCODE = 4; return }
        if ($script:downloadMode -eq 'invalid') { [IO.File]::WriteAllText($script:downloadPath, '{broken'); return }
        $old | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $script:downloadPath
        return
    }
    throw "Unexpected fake gh arguments: $args"
}
try {
    $history = Get-ReleaseHistory fixture/repo
    Assert-Equal $history.Previous.tag_name 'v0.5.3.0' 'Published baseline excludes newer draft'
    Assert-Equal (Get-NextReleaseBuildNumber $history.Builds $november) '2611R06' 'Normal release title and draft/plugin tag reserve builds'
    if ($script:calls[0] -notcontains '--paginate' -or $script:calls[0] -notcontains '--slurp') { throw 'Release history did not paginate.' }
    $baseline = Get-ReleaseBaseline fixture/repo $history $script:downloadPath
    Assert-Equal $baseline.plugins.ML '2610R09' 'Downloaded manifest parsed'
    if ($script:calls[1] -notcontains '--repo') { throw 'Download must use explicit repository.' }
    $script:downloadMode = 'failure'
    Assert-Throws { Get-ReleaseBaseline fixture/repo $history $script:downloadPath } 'failed \(4\)'
    Assert-Equal $LASTEXITCODE 4 'Download failure not reset to success'
    $script:downloadMode = 'invalid'
    Assert-Throws { Get-ReleaseBaseline fixture/repo $history $script:downloadPath } 'Conversion from JSON failed'
    $history.Previous.assets = @()
    Assert-Equal (Get-ReleaseBaseline fixture/repo $history $script:downloadPath -WarningAction SilentlyContinue) $null 'Legacy manifest absence is explicit'
    $history.Previous.name = 'Legacy release with unknown build'
    Assert-Throws { Get-ReleaseBaseline fixture/repo $history $script:downloadPath } 'neither a component manifest nor a recorded build'

    # Empty release pages are the only first-release case; transport/auth errors must fail closed.
    $script:releasePages = @(@())
    $empty = Get-ReleaseHistory fixture/repo
    Assert-Equal $empty.Previous $null 'First release'
    Assert-Equal (Get-ReleaseBaseline fixture/repo $empty $script:downloadPath) $null 'No previous manifest on first release'
    $script:apiFails = $true
    Assert-Throws { Get-ReleaseHistory fixture/repo } 'failed \(4\)'
    Assert-Throws { Assert-ReleaseTagAvailable fixture/repo v0.5.4.0 } 'failed \(4\)'
    $script:apiFails = $false

    Assert-ReleaseTagAvailable fixture/repo v0.5.4.0
    # Prefix matches must not be confused with an exact tag collision.
    $script:refs = @(@{ ref = 'refs/tags/v0.5.4.0pluginCGBBRAB' })
    Assert-ReleaseTagAvailable fixture/repo v0.5.4.0
    $script:refs = @(@{ ref = 'refs/tags/v0.5.4.0' })
    Assert-Throws { Assert-ReleaseTagAvailable fixture/repo v0.5.4.0 } 'Tag .* already exists'
    $script:refs = @()
    $script:releasePages = @(@($script:draft))
    $tag = $script:draft.tag_name
    Assert-Throws { Assert-ReleaseTagAvailable fixture/repo $tag } 'Release .* already exists'
    Assert-ReleaseTagAvailable fixture/repo $tag -AllowDraftRetry -ExpectedTitle $script:draft.name -TargetSha 'draft-sha'
    Assert-Throws { Assert-ReleaseTagAvailable fixture/repo $tag -AllowDraftRetry -ExpectedTitle $script:draft.name -TargetSha 'other-sha' } 'Release .* already exists'
    Assert-Throws { Assert-ReleaseTagAvailable fixture/repo $tag -AllowDraftRetry -ExpectedTitle 'other title' -TargetSha 'draft-sha' } 'Release .* already exists'
    $script:draft.draft = $false
    Assert-Throws { Assert-ReleaseTagAvailable fixture/repo $tag -AllowDraftRetry -ExpectedTitle $script:draft.name -TargetSha 'draft-sha' } 'Release .* already exists'

    # Execute the actual workflow metadata blocks in an isolated source tree, not copies of their logic.
    $workflow = Get-Content (Join-Path $PSScriptRoot '../.github/workflows/pre-release.yml') -Raw
    function Get-StepBody([string]$Name) {
        $pattern = '(?ms)^    - name: ' + [regex]::Escape($Name) + '\r?\n.*?^      run: \|\r?\n(?<body>.*?)(?=^    - name:|^  [\w-]+:|\z)'
        $match = [regex]::Match($workflow, $pattern)
        if (-not $match.Success) { throw "Missing release step: $Name" }
        return [regex]::Replace($match.Groups['body'].Value, '(?m)^        ', '')
    }
    $versionStep = [scriptblock]::Create((Get-StepBody 'Calculate version and build'))
    $manifestStep = [scriptblock]::Create((Get-StepBody 'Build component manifest'))
    New-Item -ItemType Directory -Path (Join-Path $fixture '.github/scripts'), (Join-Path $fixture 'src/VirtualPaper') -Force | Out-Null
    Copy-Item (Join-Path $PSScriptRoot '../.github/scripts/ReleaseMetadata.ps1'), (Join-Path $PSScriptRoot '../.github/scripts/ReleaseVersion.ps1') -Destination (Join-Path $fixture '.github/scripts')
    $variables = @('GITHUB_OUTPUT', 'PR_LABELS', 'INCREMENT', 'BUILD_NUMBER', 'HAS_PLUGIN_UPDATE', 'UPDATE_PLUGINS', 'RELEASE_BIN_DIR', 'APP_COMP_MANIFEST')
    $saved = @{}
    foreach ($name in $variables) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
    Push-Location -LiteralPath $fixture
    try {
        $month = (Get-Date).ToString('yyMM', [Globalization.CultureInfo]::InvariantCulture)
        $env:INCREMENT = 'build'
        $previous = New-ComponentManifest "${month}R07"
        $sourceVersion = '0.5.2.0'
        foreach ($mode in @('full', 'plugin')) {
            [IO.File]::WriteAllText((Join-Path $fixture 'src/VirtualPaper/VirtualPaper.csproj'), "<Project><AssemblyVersion>$sourceVersion</AssemblyVersion></Project>")
            @{ builds = @($previous.app_build_number); manifest = $previous } | ConvertTo-Json -Depth 10 | Set-Content release-baseline.json
            $env:GITHUB_OUTPUT = Join-Path $fixture "$mode-output.txt"
            $env:PR_LABELS = if ($mode -eq 'plugin') { 'version:build,update-plugin-ui' } else { 'version:build' }
            & $versionStep | Out-Null
            $outputs = @{}
            foreach ($line in Get-Content $env:GITHUB_OUTPUT) { $key, $value = $line -split '=', 2; $outputs[$key] = $value }
            $expectedBuild = if ($mode -eq 'full') { "${month}R08" } else { "${month}R09" }
            Assert-Equal $outputs.new_version '0.5.3.0' "Workflow $mode product version"
            Assert-Equal $outputs.build_number $expectedBuild "Workflow $mode build number"
            $expectedTitle = if ($mode -eq 'full') { "v0.5.3.0 (Build $expectedBuild)" } else { "v0.5.3.0 Plugin Update (Build $expectedBuild)" }
            Assert-Equal $outputs.release_title $expectedTitle "Workflow $mode title"
            if ($mode -eq 'full') { Assert-Equal $outputs.release_tag 'v0.5.3.0' 'Full release tag' }
            elseif ($outputs.release_tag -cnotmatch '^v0\.5\.3\.0plugin[A-Z]+$') { throw 'Plugin release tag lost old-client compatibility.' }
            $env:BUILD_NUMBER = $outputs.build_number
            $env:HAS_PLUGIN_UPDATE = $outputs.has_plugin_update
            $env:UPDATE_PLUGINS = $outputs.update_plugins
            & $manifestStep | Out-Null
            $json = (Get-Content $env:GITHUB_OUTPUT | Where-Object { $_.StartsWith('app_comp_manifest=') }).Substring('app_comp_manifest='.Length)
            $result = $json | ConvertFrom-Json -AsHashtable
            Assert-Equal $result.plugins.UI $expectedBuild "Workflow $mode UI build"
            Assert-Equal $result.plugins.ML "${month}R08" "Workflow $mode unselected ML build"
            $previous = $result
            $sourceVersion = $outputs.new_version
        }

        # The narrowed artifact carries the original manifest and only selected plugin payloads are repackaged.
        $env:RELEASE_BIN_DIR = Join-Path $fixture 'release'
        New-Item -ItemType Directory -Path "$env:RELEASE_BIN_DIR/Plugins/UI", "$env:RELEASE_BIN_DIR/Plugins/ML" -Force | Out-Null
        [IO.File]::WriteAllText("$env:RELEASE_BIN_DIR/Plugins/UI/UI.dll", 'fixture UI payload')
        [IO.File]::WriteAllText("$env:RELEASE_BIN_DIR/Plugins/ML/ML.dll", 'unselected payload')
        $env:APP_COMP_MANIFEST = $previous | ConvertTo-Json -Depth 10 -Compress
        & ([scriptblock]::Create((Get-StepBody 'Write component manifest')))
        $sourceHash = (Get-FileHash "$env:RELEASE_BIN_DIR/app_comp_manifest.json").Hash
        Assert-Equal (Get-FileHash app_comp_manifest.json).Hash $sourceHash 'Standalone manifest equals installed manifest'
        $packageStep = [scriptblock]::Create((Get-StepBody 'Build plugin package'))
        & $packageStep
        # A package-job retry must replace its own zip without rebuilding the application.
        & $packageStep
        Expand-Archive plugins_patch.zip extracted-patch
        $names = @(Get-ChildItem extracted-patch -File | ForEach-Object Name | Sort-Object)
        $zipName = "plugin-ui-${month}R09.zip"
        Assert-Equal ($names -join ',') ((@('app_comp_manifest.json', 'pending_update_plugins_manifest.json', $zipName) | Sort-Object) -join ',') 'Patch contents'
        Assert-Equal (Get-FileHash extracted-patch/app_comp_manifest.json).Hash $sourceHash 'Patch preserves component manifest bytes'
        $pending = Get-Content extracted-patch/pending_update_plugins_manifest.json -Raw | ConvertFrom-Json -AsHashtable
        Assert-Equal $pending.plugins.Count 1 'Only selected plugin packaged'
        Assert-Equal $pending.plugins.UI.build_number "${month}R09" 'Plugin package build matches metadata'
        Assert-Equal $pending.plugins.UI.sha256 (Get-FileHash "extracted-patch/$zipName").Hash.ToLowerInvariant() 'Plugin zip hash'
        Assert-Equal (Get-Content PLUGINS_PATCH_SHS256.txt -Raw).Trim() (Get-FileHash plugins_patch.zip).Hash.ToLowerInvariant() 'Patch hash'
        Expand-Archive "extracted-patch/$zipName" extracted-plugin
        Assert-Equal ([IO.File]::ReadAllText((Join-Path $fixture 'extracted-plugin/UI/UI.dll'))) 'fixture UI payload' 'Plugin folder layout preserved'
        $env:UPDATE_PLUGINS = 'Unknown'
        Assert-Throws { & $packageStep } 'Missing plugin directory'
        $env:UPDATE_PLUGINS = ''
        Assert-Throws { & $packageStep } 'at least one plugin'
    } finally {
        Pop-Location
        foreach ($name in $variables) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
    }
    Write-Host '[OK] Release metadata, API failures, conflicts, draft retries and actual plugin package contents/hashes/retries.'
} finally {
    Remove-Item Function:gh
    $resultsRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../TestResults'))
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($fixture)) -ne $resultsRoot) { throw 'Unsafe test cleanup path.' }
    Remove-Item -LiteralPath $fixture -Recurse -Force
    $global:LASTEXITCODE = 0
}
