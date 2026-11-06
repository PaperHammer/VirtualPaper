$ErrorActionPreference = 'Stop'

function Get-NextReleaseVersion {
    param(
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][ValidateSet('major', 'minor', 'build', 'revision')][string]$Increment
    )
    if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw "Invalid version: $Version" }
    $parts = [int[]]$Version.Split('.')
    $index = @('major', 'minor', 'build', 'revision').IndexOf($Increment)
    $parts[$index]++
    for ($i = $index + 1; $i -lt 4; $i++) { $parts[$i] = 0 }
    if (@($parts | Where-Object { $_ -gt 65534 }).Count) { throw 'AssemblyVersion component exceeds 65534.' }
    return $parts -join '.'
}

function Set-ReleaseVersion {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][string]$ExpectedVersion
    )
    foreach ($value in @($Version, $ExpectedVersion)) {
        if ($value -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw "Invalid version: $value" }
    }
    $specs = @(
        @{ Path = 'src/VirtualPaper/VirtualPaper.csproj'; Pattern = '(<AssemblyVersion>)(\d+\.\d+\.\d+\.\d+)(</AssemblyVersion>)'; Underscore = $false },
        @{ Path = 'InnoSetup/setup.iss'; Pattern = '(?m)^(#define MyAppVersion ")(\d+\.\d+\.\d+\.\d+)(")'; Underscore = $false },
        @{ Path = 'InnoSetup/setup.iss'; Pattern = '(?m)^(#define MyAppVersionText ")(\d+_\d+_\d+_\d+)(")'; Underscore = $true },
        @{ Path = 'README.md'; Pattern = '(img\.shields\.io/badge/release-v)(\d+\.\d+\.\d+\.\d+)(-blue)'; Underscore = $false }
    )
    $pending = @{}
    foreach ($spec in $specs) {
        $path = Join-Path $Root $spec.Path
        $content = if ($pending.ContainsKey($path)) { $pending[$path] } else { [IO.File]::ReadAllText($path) }
        $matches = [regex]::Matches($content, $spec.Pattern)
        if ($matches.Count -ne 1) { throw "Expected exactly one version field: $($spec.Path) / $($spec.Pattern)" }
        $current = $matches[0].Groups[2].Value.Replace('_', '.')
        if ($current -ne $ExpectedVersion -and $current -ne $Version) {
            throw "Unexpected version $current in $($spec.Path); expected $ExpectedVersion or $Version."
        }
        $target = if ($spec.Underscore) { $Version.Replace('.', '_') } else { $Version }
        $pending[$path] = [regex]::Replace($content, $spec.Pattern, [Text.RegularExpressions.MatchEvaluator]{
            param($match)
            $match.Groups[1].Value + $target + $match.Groups[3].Value
        })
    }
    # Validate every field before writing any file. Preserve all unrelated content and line endings.
    foreach ($path in $pending.Keys) { [IO.File]::WriteAllText($path, $pending[$path], [Text.UTF8Encoding]::new($false)) }
}

function Invoke-ReleaseGit {
    param([Parameter(Mandatory)][string[]]$GitArguments)
    $output = & git @GitArguments
    if ($LASTEXITCODE -ne 0) { throw "git $($GitArguments -join ' ') failed ($LASTEXITCODE)." }
    return $output
}

function Sync-ReleaseVersion {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$MergeSha,
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][string]$ExpectedVersion
    )
    Push-Location -LiteralPath $Root
    try {
        if (Invoke-ReleaseGit @('status', '--porcelain')) { throw 'Version sync requires a clean checkout.' }
        Invoke-ReleaseGit @('fetch', 'origin', 'main', 'dev') | Out-Host
        $message = "[Github CI] chore: bump version to $Version"
        $mainSha = Invoke-ReleaseGit @('rev-parse', 'origin/main')
        if ($mainSha -ne $MergeSha) {
            # A retry may encounter our completed atomic push. Reject all unrelated newer commits.
            $parent = Invoke-ReleaseGit @('rev-parse', 'origin/main^')
            $subject = Invoke-ReleaseGit @('log', '-1', '--format=%s', 'origin/main')
            if ($parent -ne $MergeSha -or $subject -ne $message) { throw 'main changed; publish the latest merged PR instead.' }
            $changed = @(Invoke-ReleaseGit @('diff', '--name-only', "$MergeSha..origin/main"))
            if (@($changed | Where-Object { $_ -notin @('src/VirtualPaper/VirtualPaper.csproj', 'InnoSetup/setup.iss', 'README.md') }).Count) {
                throw 'The existing bump commit changed files outside the version sync scope.'
            }
        }
        Invoke-ReleaseGit @('checkout', '-B', 'main', 'origin/main') | Out-Host
        Set-ReleaseVersion -Root $Root -Version $Version -ExpectedVersion $ExpectedVersion
        if (Invoke-ReleaseGit @('status', '--porcelain')) {
            Invoke-ReleaseGit @('add', 'src/VirtualPaper/VirtualPaper.csproj', 'InnoSetup/setup.iss', 'README.md') | Out-Host
            Invoke-ReleaseGit @('commit', '-m', $message) | Out-Host
        }

        # Share main's release history instead of creating a second version commit on dev.
        Invoke-ReleaseGit @('checkout', '-B', 'dev', 'origin/dev') | Out-Host
        try {
            Invoke-ReleaseGit @('merge', '--no-edit', 'main') | Out-Host
        }
        catch {
            if (Test-Path -LiteralPath (Invoke-ReleaseGit @('rev-parse', '--git-path', 'MERGE_HEAD'))) {
                Invoke-ReleaseGit @('merge', '--abort') | Out-Host
            }
            throw
        }
        # Prepare both refs locally; a conflict or rejected atomic push changes neither remote ref.
        Invoke-ReleaseGit @('push', '--atomic', 'origin', 'main', 'dev') | Out-Host
        return Invoke-ReleaseGit @('rev-parse', 'main')
    } finally { Pop-Location }
}
