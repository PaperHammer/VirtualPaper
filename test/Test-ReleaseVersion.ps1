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
    [IO.File]::WriteAllText((Join-Path $repo 'main-delete.txt'), 'shared file')
    [IO.File]::WriteAllText((Join-Path $repo 'dev 删除 [main].txt'), 'shared file')
    [IO.File]::WriteAllText((Join-Path $repo 'rename.txt'), 'shared rename content')
    $mixedBase = ((1..30 | ForEach-Object { "line $_" }) -join "`n") + "`n"
    [IO.File]::WriteAllText((Join-Path $repo 'mixed.txt'), $mixedBase)
    [IO.File]::WriteAllBytes((Join-Path $repo 'binary.bin'), [byte[]]@(0, 1, 2))
    Invoke-ReleaseGit @('add', '.')
    Invoke-ReleaseGit @('commit', '-m', 'initial') | Out-Null
    Invoke-ReleaseGit @('checkout', '-b', 'dev') | Out-Null
    $initialMergeSha = Invoke-ReleaseGit @('rev-parse', 'main')
    Invoke-ReleaseGit @('push', 'origin', 'main', 'dev') | Out-Null
    $initialBump = Sync-ReleaseVersion -Root $repo -MergeSha $initialMergeSha -Version '0.5.2.1' -ExpectedVersion '0.5.2.0'
    $initialParents = (Invoke-ReleaseGit @('rev-list', '--parents', '-1', 'origin/dev')).Split(' ')
    Assert-Equal $initialParents.Count 3 'Even a fast-forwardable back-merge records a merge commit'
    Assert-Equal $initialParents[1] $initialMergeSha 'Initial merge preserves the original dev head'
    Assert-Equal $initialParents[2] $initialBump 'Initial merge includes the main version commit'
    # dev can advance while a release is packaging. Its new code and same-file edits must survive.
    [IO.File]::AppendAllText((Join-Path $repo 'README.md'), "new dev documentation`r`n")
    [IO.File]::WriteAllText((Join-Path $repo 'feature.txt'), 'new dev feature')
    Invoke-ReleaseGit @('add', '.')
    Invoke-ReleaseGit @('commit', '-m', 'new feature') | Out-Null
    $previousDevSha = Invoke-ReleaseGit @('rev-parse', 'HEAD')
    # Both main's release history and later main-only fixes must return to dev.
    Invoke-ReleaseGit @('checkout', 'main') | Out-Null
    [IO.File]::WriteAllText((Join-Path $repo 'release-only.txt'), 'main release history')
    Invoke-ReleaseGit @('add', 'release-only.txt')
    [IO.File]::WriteAllText((Join-Path $repo 'main-only.txt'), 'main hotfix')
    Invoke-ReleaseGit @('add', 'main-only.txt')
    Invoke-ReleaseGit @('commit', '-m', 'prepare release and main hotfix') | Out-Null
    $mergeSha = Invoke-ReleaseGit @('rev-parse', 'HEAD')
    Invoke-ReleaseGit @('push', 'origin', 'main', 'dev') | Out-Null

    $sha = Sync-ReleaseVersion -Root $repo -MergeSha $mergeSha -Version '0.5.3.0' -ExpectedVersion '0.5.2.1'
    Assert-Equal (Invoke-ReleaseGit @('rev-parse', 'origin/main')) $sha 'Published main SHA'
    Assert-Equal (Invoke-ReleaseGit @('show', 'origin/main:feature.txt')) 'main feature' 'main business code preserved'
    Assert-Equal (Invoke-ReleaseGit @('show', 'origin/dev:feature.txt')) 'new dev feature' 'New dev code preserved'
    Assert-Equal (Invoke-ReleaseGit @('show', 'origin/dev:release-only.txt')) 'main release history' 'Main changes merged into dev'
    Assert-Equal (Invoke-ReleaseGit @('show', 'origin/dev:main-only.txt')) 'main hotfix' 'main changes merged back to dev'
    Assert-Equal (Invoke-ReleaseGit @('rev-parse', 'origin/main^')) $mergeSha 'Version commit is based on the checked main'
    $parents = (Invoke-ReleaseGit @('rev-list', '--parents', '-1', 'origin/dev')).Split(' ')
    Assert-Equal $parents.Count 3 'dev has a real two-parent merge commit'
    Assert-Equal $parents[1] $previousDevSha 'Merge preserves the previous dev head'
    Assert-Equal $parents[2] $sha 'Merge includes the exact main version commit'
    Invoke-ReleaseGit @('merge-base', '--is-ancestor', 'origin/main', 'origin/dev') | Out-Null
    $readme = (Invoke-ReleaseGit @('show', 'origin/dev:README.md')) -join "`n"
    if (-not $readme.Contains('new dev documentation') -or -not $readme.Contains('release-v0.5.3.0-blue')) {
        throw 'Version synchronization lost dev README changes.'
    }
    $bytes = [IO.File]::ReadAllText((Join-Path $repo 'InnoSetup/setup.iss'))
    Assert-Equal $bytes "#define MyAppVersion `"0.5.3.0`"`r`n#define MyAppVersionText `"0_5_3_0`"`r`n; keep setup`r`n" 'Non-version bytes and CRLF preserved'
    $devSha = Invoke-ReleaseGit @('rev-parse', 'origin/dev')
    $retry = Sync-ReleaseVersion -Root $repo -MergeSha $mergeSha -Version '0.5.3.0' -ExpectedVersion '0.5.2.1'
    Assert-Equal $retry $sha 'Retry keeps target version and main SHA'
    Assert-Equal (Invoke-ReleaseGit @('rev-parse', 'origin/dev')) $devSha 'Retry creates no dev commit'
    # Retrying after more dev work must preserve it and must not create a duplicate merge.
    [IO.File]::WriteAllText((Join-Path $repo 'later-dev.txt'), 'later development')
    Invoke-ReleaseGit @('add', 'later-dev.txt')
    Invoke-ReleaseGit @('commit', '-m', 'later dev work') | Out-Null
    Invoke-ReleaseGit @('push', 'origin', 'dev') | Out-Null
    $devSha = Invoke-ReleaseGit @('rev-parse', 'origin/dev')
    Assert-Equal (Sync-ReleaseVersion -Root $repo -MergeSha $mergeSha -Version '0.5.3.0' -ExpectedVersion '0.5.2.1') $sha 'Retry after dev advancement keeps main'
    Assert-Equal (Invoke-ReleaseGit @('rev-parse', 'origin/dev')) $devSha 'Retry preserves later dev history'
    Assert-Throws { Sync-ReleaseVersion -Root $repo -MergeSha $mergeSha -Version '0.5.4.0' -ExpectedVersion '0.5.2.1' } 'Stale release rejected'

    # A failed validation must not partially change any local file.
    [IO.File]::WriteAllText((Join-Path $repo 'README.md'), 'missing badge')
    $before = [IO.File]::ReadAllText((Join-Path $repo 'InnoSetup/setup.iss'))
    Assert-Throws { Set-ReleaseVersion $repo '0.5.4.0' '0.5.3.0' } 'Missing version field'
    Assert-Equal ([IO.File]::ReadAllText((Join-Path $repo 'InnoSetup/setup.iss'))) $before 'Validate before writing'
    Invoke-ReleaseGit @('restore', 'README.md')
    Assert-Throws { Set-ReleaseVersion $repo '0.5.4.0' '0.5.1.0' } 'Unexpected version rejected'

    # The next dev -> main release can fast-forward without resolving the old version change.
    Invoke-ReleaseGit @('checkout', '-B', 'main', 'origin/main') | Out-Null
    Invoke-ReleaseGit @('merge', '--ff-only', 'origin/dev') | Out-Null
    Invoke-ReleaseGit @('push', 'origin', 'main') | Out-Null
    $nextMergeSha = Invoke-ReleaseGit @('rev-parse', 'main')
    $sha = Sync-ReleaseVersion -Root $repo -MergeSha $nextMergeSha -Version '0.5.4.0' -ExpectedVersion '0.5.3.0'
    $devSha = Invoke-ReleaseGit @('rev-parse', 'origin/dev')
    Assert-Equal (Invoke-ReleaseGit @('rev-parse', 'origin/dev^2')) $sha 'Next release back-merge includes the new main version commit'
    Invoke-ReleaseGit @('merge-base', '--is-ancestor', 'origin/main', 'origin/dev') | Out-Null
    Assert-Equal (Invoke-ReleaseGit @('rev-parse', 'origin/main^')) $nextMergeSha 'Next release retains the shared history'

    # Reject dev on the server: --atomic must also leave main untouched.
    $hookPath = Join-Path $remote 'hooks/update'
    [IO.File]::WriteAllText($hookPath, "#!/bin/sh`nif [ `"`$1`" = refs/heads/dev ]; then exit 1; fi`nexit 0`n", [Text.UTF8Encoding]::new($false))
    if (-not $IsWindows) { & chmod +x $hookPath }
    Assert-Throws { Sync-ReleaseVersion -Root $repo -MergeSha $sha -Version '0.5.5.0' -ExpectedVersion '0.5.4.0' } 'Atomic push rejected'
    Invoke-ReleaseGit @('fetch', 'origin', 'main', 'dev') | Out-Null
    Assert-Equal (Invoke-ReleaseGit @('rev-parse', 'origin/main')) $sha 'Rejected push leaves main unchanged'
    Assert-Equal (Invoke-ReleaseGit @('rev-parse', 'origin/dev')) $devSha 'Rejected push leaves dev unchanged'
    # Main wins conflicting hunks and file states; all other dev work survives.
    Remove-Item -LiteralPath $hookPath
    Invoke-ReleaseGit @('checkout', '-B', 'main', 'origin/main') | Out-Null
    [IO.File]::WriteAllText((Join-Path $repo 'conflict.txt'), 'main conflict content')
    Invoke-ReleaseGit @('rm', 'main-delete.txt') | Out-Null
    [IO.File]::WriteAllText((Join-Path $repo 'dev 删除 [main].txt'), 'main modified content')
    [IO.File]::WriteAllText((Join-Path $repo 'mixed.txt'), $mixedBase.Replace("line 2`n", "main line 2`n"))
    [IO.File]::WriteAllBytes((Join-Path $repo 'binary.bin'), [byte[]]@(0, 3, 4))
    Invoke-ReleaseGit @('mv', 'rename.txt', 'main-renamed.txt') | Out-Null
    Invoke-ReleaseGit @('add', '.')
    Invoke-ReleaseGit @('commit', '-m', 'main conflict') | Out-Null
    $conflictMain = Invoke-ReleaseGit @('rev-parse', 'HEAD')
    Invoke-ReleaseGit @('checkout', '-B', 'dev', 'origin/dev') | Out-Null
    [IO.File]::WriteAllText((Join-Path $repo 'conflict.txt'), 'dev conflict content')
    [IO.File]::WriteAllText((Join-Path $repo 'main-delete.txt'), 'dev modified content')
    Invoke-ReleaseGit @('--literal-pathspecs', 'rm', '--', 'dev 删除 [main].txt') | Out-Null
    [IO.File]::WriteAllText((Join-Path $repo 'mixed.txt'), $mixedBase.Replace("line 2`n", "dev line 2`n").Replace("line 25`n", "dev line 25`n"))
    [IO.File]::WriteAllBytes((Join-Path $repo 'binary.bin'), [byte[]]@(0, 5, 6))
    Invoke-ReleaseGit @('mv', 'rename.txt', 'dev-renamed.txt') | Out-Null
    Invoke-ReleaseGit @('add', '.')
    Invoke-ReleaseGit @('commit', '-m', 'dev conflict') | Out-Null
    $conflictDev = Invoke-ReleaseGit @('rev-parse', 'HEAD')
    Invoke-ReleaseGit @('push', 'origin', 'main', 'dev') | Out-Null
    $preferredMain = Sync-ReleaseVersion -Root $repo -MergeSha $conflictMain -Version '0.5.5.0' -ExpectedVersion '0.5.4.0'
    Assert-Equal (Invoke-ReleaseGit @('rev-parse', 'origin/main')) $preferredMain 'Resolved merge publishes main bump'
    Assert-Equal (Invoke-ReleaseGit @('rev-parse', 'origin/dev^1')) $conflictDev 'Resolved merge preserves dev history'
    Assert-Equal (Invoke-ReleaseGit @('rev-parse', 'origin/dev^2')) $preferredMain 'Resolved merge contains main bump'
    Assert-Equal ([IO.File]::ReadAllText((Join-Path $repo 'conflict.txt'))) 'main conflict content' 'Main wins add/add conflict'
    Assert-Equal (Test-Path -LiteralPath (Join-Path $repo 'main-delete.txt')) $false 'Main deletion wins over dev modification'
    Assert-Equal ([IO.File]::ReadAllText((Join-Path $repo 'dev 删除 [main].txt'))) 'main modified content' 'Main modification wins over dev deletion with a literal Unicode path'
    $mixed = [IO.File]::ReadAllText((Join-Path $repo 'mixed.txt'))
    if (-not $mixed.Contains("main line 2`n") -or -not $mixed.Contains("dev line 25`n")) { throw 'Conflict preference discarded a nonconflicting dev hunk.' }
    Assert-Equal (Invoke-ReleaseGit @('rev-parse', 'origin/dev:binary.bin')) (Invoke-ReleaseGit @('rev-parse', 'origin/main:binary.bin')) 'Main wins binary conflict'
    Assert-Equal (Test-Path -LiteralPath (Join-Path $repo 'rename.txt')) $false 'Old rename path is removed'
    Assert-Equal (Test-Path -LiteralPath (Join-Path $repo 'dev-renamed.txt')) $false 'Dev conflicting rename is removed'
    Assert-Equal ([IO.File]::ReadAllText((Join-Path $repo 'main-renamed.txt'))) 'shared rename content' 'Main wins rename/rename conflict'
    Assert-Equal ([IO.File]::ReadAllText((Join-Path $repo 'feature.txt'))) 'new dev feature' 'Unrelated dev feature survives conflict resolution'
    Assert-Equal ([IO.File]::ReadAllText((Join-Path $repo 'later-dev.txt'))) 'later development' 'Later dev work survives conflict resolution'
    Assert-Equal (Invoke-ReleaseGit @('status', '--porcelain')) $null 'Resolved merge leaves a clean checkout'
    $mergeState = Invoke-ReleaseGit @('rev-parse', '--git-path', 'MERGE_HEAD')
    if (Test-Path -LiteralPath $mergeState) { throw 'Resolved back-merge left MERGE_HEAD behind.' }
    $resolvedDev = Invoke-ReleaseGit @('rev-parse', 'origin/dev')
    Assert-Equal (Sync-ReleaseVersion -Root $repo -MergeSha $conflictMain -Version '0.5.5.0' -ExpectedVersion '0.5.4.0') $preferredMain 'Retry reuses main after conflict resolution'
    Assert-Equal (Invoke-ReleaseGit @('rev-parse', 'origin/dev')) $resolvedDev 'Retry does not duplicate a resolved merge'

    # A Git failure while committing the back-merge must abort it without advancing either remote.
    $commitHook = Join-Path $repo '.git/hooks/prepare-commit-msg'
    [IO.File]::WriteAllText($commitHook, "#!/bin/sh`nif test -f .git/MERGE_HEAD; then exit 1; fi`nexit 0`n", [Text.UTF8Encoding]::new($false))
    if (-not $IsWindows) { & chmod +x $commitHook }
    Assert-Throws { Sync-ReleaseVersion -Root $repo -MergeSha $preferredMain -Version '0.5.6.0' -ExpectedVersion '0.5.5.0' } 'Back-merge commit failure rejected'
    Invoke-ReleaseGit @('fetch', 'origin', 'main', 'dev') | Out-Null
    Assert-Equal (Invoke-ReleaseGit @('rev-parse', 'origin/main')) $preferredMain 'Git failure leaves remote main unchanged'
    Assert-Equal (Invoke-ReleaseGit @('rev-parse', 'origin/dev')) $resolvedDev 'Git failure leaves remote dev unchanged'
    Assert-Equal ((Invoke-ReleaseGit @('status', '--porcelain')) -join '') '' 'Git failure restores a clean checkout'
    if (Test-Path -LiteralPath (Invoke-ReleaseGit @('rev-parse', '--git-path', 'MERGE_HEAD'))) {
        throw 'A rejected synchronization left an unfinished merge.'
    }
    Write-Host '[OK] Main bump/dev ancestry, next release, main-preferred conflicts, dev preservation, retries, failed-merge cleanup and atomic push rejection.'
} finally {
    Pop-Location
    $resolvedFixture = [IO.Path]::GetFullPath($fixture)
    if ([IO.Path]::GetDirectoryName($resolvedFixture) -ne $resultsRoot) { throw 'Unsafe test cleanup path.' }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
}
