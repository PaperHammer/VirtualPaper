$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../.github/scripts/ReleaseVersion.ps1')

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -cne $Expected) { throw "$Message`: expected [$Expected], got [$Actual]." }
}
function Assert-Throws([scriptblock]$Action, [string]$Message) {
    $threw = $false
    try { & $Action | Out-Null } catch { $threw = $true }
    if (-not $threw) { throw "Expected failure: $Message" }
}
foreach ($case in @(
    @('major', '1.0.0.0'), @('minor', '0.6.0.0'),
    @('build', '0.5.3.0'), @('revision', '0.5.2.10')
)) {
    Assert-Equal (Get-NextReleaseVersion '0.5.2.9' $case[0]) $case[1] "Increment $($case[0])"
}
Assert-Throws { Get-NextReleaseVersion '0.5.2' 'build' } 'invalid version'
Assert-Throws { Get-NextReleaseVersion '65534.0.0.0' 'major' } 'AssemblyVersion overflow'

$resultsRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../TestResults'))
$fixture = Join-Path $resultsRoot "release-version-$([guid]::NewGuid().ToString('N'))"
$repo = Join-Path $fixture 'publisher'
$remote = Join-Path $fixture 'remote.git'
New-Item -ItemType Directory -Path $repo -Force | Out-Null
Push-Location -LiteralPath $repo
try {
    Invoke-ReleaseGit @('init', '--bare', $remote) | Out-Null
    Invoke-ReleaseGit @('init', '--initial-branch=main') | Out-Null
    Invoke-ReleaseGit @('config', 'user.email', 'test@example.invalid')
    Invoke-ReleaseGit @('config', 'user.name', 'Release test')
    Invoke-ReleaseGit @('config', 'core.autocrlf', 'false')
    Invoke-ReleaseGit @('remote', 'add', 'origin', $remote)
    New-Item -ItemType Directory -Path 'src/VirtualPaper', 'InnoSetup' -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $repo 'src/VirtualPaper/VirtualPaper.csproj'), "<Project>`r`n<AssemblyVersion>0.5.2.0</AssemblyVersion>`r`n<!-- keep main -->`r`n</Project>`r`n")
    [IO.File]::WriteAllText((Join-Path $repo 'InnoSetup/setup.iss'), "#define MyAppVersion `"0.5.2.0`"`r`n#define MyAppVersionText `"0_5_2_0`"`r`n; keep setup`r`n")
    [IO.File]::WriteAllText((Join-Path $repo 'README.md'), "https://img.shields.io/badge/release-v0.5.2.0-blue`r`nkeep README`r`n")
    [IO.File]::WriteAllText((Join-Path $repo 'feature.txt'), 'main feature')
    Invoke-ReleaseGit @('add', '.')
    Invoke-ReleaseGit @('commit', '-m', 'initial') | Out-Null
    $mergeSha = Invoke-ReleaseGit @('rev-parse', 'HEAD')
    Invoke-ReleaseGit @('checkout', '-b', 'dev') | Out-Null
    # dev can advance while a release is packaging. Its new code and same-file edits must survive.
    [IO.File]::AppendAllText((Join-Path $repo 'README.md'), "new dev documentation`r`n")
    [IO.File]::WriteAllText((Join-Path $repo 'feature.txt'), 'new dev feature')
    Invoke-ReleaseGit @('add', '.')
    Invoke-ReleaseGit @('commit', '-m', 'new feature') | Out-Null
    Invoke-ReleaseGit @('push', 'origin', 'main', 'dev') | Out-Null

    $sha = Sync-ReleaseVersion -Root $repo -MergeSha $mergeSha -Version '0.5.3.0' -ExpectedVersion '0.5.2.0'
    Assert-Equal (Invoke-ReleaseGit @('rev-parse', 'origin/main')) $sha 'Published main SHA'
    Assert-Equal (Invoke-ReleaseGit @('show', 'origin/main:feature.txt')) 'main feature' 'main business code preserved'
    Assert-Equal (Invoke-ReleaseGit @('show', 'origin/dev:feature.txt')) 'new dev feature' 'New dev code preserved'
    $readme = (Invoke-ReleaseGit @('show', 'origin/dev:README.md')) -join "`n"
    if (-not $readme.Contains('new dev documentation') -or -not $readme.Contains('release-v0.5.3.0-blue')) {
        throw 'Version synchronization lost dev README changes.'
    }
    $bytes = [IO.File]::ReadAllText((Join-Path $repo 'InnoSetup/setup.iss'))
    Assert-Equal $bytes "#define MyAppVersion `"0.5.3.0`"`r`n#define MyAppVersionText `"0_5_3_0`"`r`n; keep setup`r`n" 'Non-version bytes and CRLF preserved'
    $devSha = Invoke-ReleaseGit @('rev-parse', 'origin/dev')
    $retry = Sync-ReleaseVersion -Root $repo -MergeSha $mergeSha -Version '0.5.3.0' -ExpectedVersion '0.5.2.0'
    Assert-Equal $retry $sha 'Retry keeps target version and main SHA'
    Assert-Equal (Invoke-ReleaseGit @('rev-parse', 'origin/dev')) $devSha 'Retry creates no dev commit'
    Assert-Throws { Sync-ReleaseVersion -Root $repo -MergeSha $mergeSha -Version '0.5.4.0' -ExpectedVersion '0.5.2.0' } 'Stale release rejected'

    # A failed validation must not partially change any local file.
    [IO.File]::WriteAllText((Join-Path $repo 'README.md'), 'missing badge')
    $before = [IO.File]::ReadAllText((Join-Path $repo 'InnoSetup/setup.iss'))
    Assert-Throws { Set-ReleaseVersion $repo '0.5.4.0' '0.5.3.0' } 'Missing version field'
    Assert-Equal ([IO.File]::ReadAllText((Join-Path $repo 'InnoSetup/setup.iss'))) $before 'Validate before writing'
    Invoke-ReleaseGit @('restore', 'README.md')
    Assert-Throws { Set-ReleaseVersion $repo '0.5.4.0' '0.5.1.0' } 'Unexpected version rejected'

    # Reject dev on the server: --atomic must also leave main untouched.
    $hookPath = Join-Path $remote 'hooks/update'
    [IO.File]::WriteAllText($hookPath, "#!/bin/sh`nif [ `"`$1`" = refs/heads/dev ]; then exit 1; fi`nexit 0`n", [Text.UTF8Encoding]::new($false))
    if (-not $IsWindows) { & chmod +x $hookPath }
    Assert-Throws { Sync-ReleaseVersion -Root $repo -MergeSha $sha -Version '0.5.4.0' -ExpectedVersion '0.5.3.0' } 'Atomic push rejected'
    Invoke-ReleaseGit @('fetch', 'origin', 'main', 'dev') | Out-Null
    Assert-Equal (Invoke-ReleaseGit @('rev-parse', 'origin/main')) $sha 'Rejected push leaves main unchanged'
    Assert-Equal (Invoke-ReleaseGit @('rev-parse', 'origin/dev')) $devSha 'Rejected push leaves dev unchanged'
    Write-Host '[OK] Four version increments, validation, version-only sync, dev preservation, idempotent retry and atomic push rejection.'
} finally {
    Pop-Location
    $resolvedFixture = [IO.Path]::GetFullPath($fixture)
    if ([IO.Path]::GetDirectoryName($resolvedFixture) -ne $resultsRoot) { throw 'Unsafe test cleanup path.' }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
}
