# Called by validate-test-workflow.ps1 after loading its workflow/job helpers.
foreach ($text in @($workflow, $releaseWorkflow, $acceptanceWorkflow)) {
    if ($text -notmatch '(?m)^defaults:\r?\n  run:\r?\n    shell: pwsh\s*$') {
        throw 'All workflow PowerShell run blocks, including Ubuntu gates/summaries, require the pwsh default.'
    }
}
$sharedNamePairs = @{ 'build-solution' = 'build-release' }
foreach ($id in @('package-exe', 'package-msix', 'packages-ready', 'test-exe-acceptance', 'test-msix-acceptance', 'summarize-tests') + $specs.Id) {
    $sharedNamePairs[$id] = $id
}
foreach ($pair in $sharedNamePairs.GetEnumerator()) {
    $checkName = [regex]::Match((Get-WorkflowJob $pair.Key $workflow), '(?m)^    name: ([^\r\n]+)').Groups[1].Value.Trim()
    $releaseName = [regex]::Match((Get-WorkflowJob $pair.Value $releaseWorkflow), '(?m)^    name: ([^\r\n]+)').Groups[1].Value.Trim()
    if (-not $checkName -or $checkName -cne $releaseName) {
        throw "Shared stages must have matching display names: $($pair.Key) / $($pair.Value)."
    }
}
foreach ($text in @($workflow, $releaseWorkflow)) {
    if ($text -notmatch '(?m)^  workflow_dispatch:' -or $text -match '(?m)^  (push|pull_request|pull_request_target|workflow_run|schedule|workflow_call):') {
        throw 'Both workflows must remain manual PR-selected workflows.'
    }
    if ($text -match '(?m)^    environment:|continue-on-error:|AllowUIFailure|UIJobResult' -or -not $text.Contains('pr_number:')) {
        throw 'Package, project and installed UI jobs must all be strict.'
    }
    $isRelease = $text -ceq $releaseWorkflow
    $metadata = if ($isRelease) { 'prepare-metadata' } else { 'validate-pr' }
    $buildId = if ($isRelease) { 'build-release' } else { 'build-solution' }
    $artifactName = if ($isRelease) { 'build-release-output' } else { 'build-output' }
    $gates = @($metadata, $buildId)
    Assert-JobNeeds $buildId @($metadata) $text
    foreach ($id in @('package-exe', 'package-msix')) { Assert-JobNeeds $id $gates $text }
    Assert-JobNeeds 'packages-ready' ($gates + @('package-exe', 'package-msix')) $text
    $ready = Get-WorkflowJob 'packages-ready' $text
    if (-not $ready.Contains('if: always() && !cancelled()') -or -not $ready.Contains("'success' } else { 'skipped'")) {
        throw 'The package barrier must reject failed packages and distinguish a disabled Store channel.'
    }
    foreach ($spec in $specs) {
        $project = "src/$($spec.Project)/$($spec.Project).csproj"
        if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $project)) -or -not $solution.Contains("$($spec.Project)\$($spec.Project).csproj")) {
            throw "Test project missing from solution: $project"
        }
        Assert-JobNeeds $spec.Id ($gates + 'packages-ready') $text
        $job = Get-WorkflowJob $spec.Id $text
        if ($job -match '(?m)^    if:' -or -not $job.Contains('uses: ./test/actions/run-project') -or
            -not $job.Contains("name: $artifactName") -or -not $job.Contains("require-complete-execution: 'true'")) {
            throw 'Project jobs must reuse the fixed build after packaging and reject skipped tests.'
        }
    }
    $tests = @('test-exe-acceptance', 'test-msix-acceptance')
    foreach ($channel in @('exe', 'msix')) {
        $id = "test-$channel-acceptance"
        Assert-JobNeeds $id ($gates + 'packages-ready' + $specs.Id) $text
        $job = Get-WorkflowJob $id $text
        $shaOutput = if ($isRelease) { 'main_sha' } else { 'test_sha' }
        $sourceInput = 'source-sha: ${{ needs.' + $metadata + '.outputs.' + $shaOutput + ' }}'
        if (-not $job.Contains('uses: ./.github/workflows/package-acceptance.yml') -or
            -not $job.Contains("channel: $channel") -or -not $job.Contains("build-artifact: $artifactName") -or
            -not $job.Contains($sourceInput) -or $job -match '(?m)^    (runs-on|steps|strategy|secrets):' -or
            ($channel -eq 'exe' -and $job -match '(?m)^    if:')) {
            throw 'Each channel must call the shared acceptance workflow after all project jobs, using the same frozen build/source.'
        }
    }
    $build = Get-WorkflowJob $buildId $text
    foreach ($hostPath in @('test/VirtualPaper.UIOperationTest/bin/x64/Release/', 'test/VirtualPaper.InstallerTest/bin/Release/', 'test/actions/run-project/action.yml')) {
        if (-not $build.Contains($hostPath)) { throw "Shared build omits $hostPath" }
    }
    if ($build -match 'dotnet test |node .github/scripts/test-') { throw 'Application tests must remain independent of the build.' }
    $summary = Get-WorkflowJob 'summarize-tests' $text
    Assert-JobNeeds 'summarize-tests' ($gates + @('package-exe', 'package-msix', 'packages-ready') + $specs.Id + $tests) $text
    if (-not $summary.Contains('./test/Summarize-PipelineTests.ps1') -or -not $summary.Contains('${{ toJSON(needs) }}') -or
        -not $summary.Contains('pattern: test-results-*')) { throw 'Summary must validate raw reports, package identities and job outcomes.' }
    foreach ($id in @('package-msix', 'test-msix-acceptance')) {
        $job = Get-WorkflowJob $id $text
        if ($isRelease) {
            if (-not $job.Contains("if: needs.prepare-metadata.outputs.msstore_enabled == 'true'")) { throw 'Release MSIX jobs must be selected by the frozen PR label.' }
        } elseif ($job -match '(?m)^    if:') { throw 'Pre-check must always validate both package channels.' }
    }
}
if ($acceptanceWorkflow -notmatch '(?m)^  workflow_call:' -or
    $acceptanceWorkflow -match '(?m)^  (push|pull_request|pull_request_target|workflow_run|workflow_dispatch|schedule):' -or
    $acceptanceWorkflow -match 'continue-on-error:|AllowUIFailure|UIJobResult|secrets:|New-CIMsixPackage|Build-Msix|Run-ProjectTests|dotnet test ') {
    throw 'Installed acceptance must be a strict reusable workflow that only consumes existing packages/builds.'
}
$acceptance = Get-WorkflowJob 'installed' $acceptanceWorkflow
$jobSection = ($acceptanceWorkflow -split '(?m)^jobs:\r?\n', 2)[1]
if ([regex]::Matches($jobSection, '(?m)^  [\w-]+:').Count -ne 1 -or
    $acceptance -notmatch '(?m)^    runs-on: windows-latest\s*$' -or
    -not $acceptance.Contains("`$env:RUNNER_ENVIRONMENT -cne 'github-hosted'") -or
    -not $acceptance.Contains('ref: ${{ inputs.source-sha }}') -or
    -not $acceptance.Contains('name: ${{ inputs.build-artifact }}') -or
    -not $acceptance.Contains('name: package-${{ inputs.channel }}') -or
    -not $acceptance.Contains('Get-PackageArtifact "PackageArtifacts/$env:CHANNEL/package.json" $env:CHANNEL $env:SOURCE_SHA')) {
    throw 'Each channel invocation must own one fresh hosted Windows VM and validate the frozen package before installation.'
}
$acceptanceSteps = @([regex]::Matches($acceptance, '(?ms)^    - name: [^\r\n]+.*?(?=^    - name: |\z)') | ForEach-Object Value)
# Execute the workflow's actual first-step guard without touching installations or a desktop.
$guardMatch = [regex]::Match($acceptanceSteps[0], '(?ms)^      run: \|\r?\n(.*)\z')
if (-not $guardMatch.Success) { throw 'The runner/input guard must precede all installation steps.' }
$guard = [scriptblock]::Create([regex]::Replace($guardMatch.Groups[1].Value, '(?m)^        ', ''))
$guardEnvironment = @{}
foreach ($name in @('CHANNEL', 'SOURCE_SHA', 'BUILD_ARTIFACT', 'RUNNER_ENVIRONMENT')) {
    $guardEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}
try {
    $env:SOURCE_SHA = 'a' * 40
    $env:RUNNER_ENVIRONMENT = 'github-hosted'
    foreach ($channel in @('exe', 'msix')) {
        $env:CHANNEL = $channel
        foreach ($artifact in @('build-output', 'build-release-output')) { $env:BUILD_ARTIFACT = $artifact; & $guard }
    }
    foreach ($invalid in @(
        @{ CHANNEL = 'unknown' }, @{ SOURCE_SHA = 'main' },
        @{ BUILD_ARTIFACT = 'unrelated-build' }, @{ RUNNER_ENVIRONMENT = 'self-hosted' }
    )) {
        $env:CHANNEL = 'exe'; $env:SOURCE_SHA = 'a' * 40
        $env:BUILD_ARTIFACT = 'build-output'; $env:RUNNER_ENVIRONMENT = 'github-hosted'
        foreach ($name in $invalid.Keys) { [Environment]::SetEnvironmentVariable($name, $invalid[$name]) }
        $rejected = $false
        try { & $guard } catch { $rejected = $true }
        if (-not $rejected) { throw 'The acceptance workflow allowed an invalid input or a shared self-hosted runner.' }
    }
} finally {
    foreach ($name in $guardEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $guardEnvironment[$name]) }
}
Write-Host '[OK] The actual reusable-workflow guard accepts both channels/builds and rejects self-hosted or invalid inputs.'
foreach ($channel in @('exe', 'msix')) {
    $smoke = @($acceptanceSteps | Where-Object { $_ -match "(?m)^      id: $channel-smoke\s*$" })
    $ui = @($acceptanceSteps | Where-Object { $_ -match "(?m)^      id: $channel-ui\s*$" })
    if ($smoke.Count -ne 1 -or $ui.Count -ne 1 -or
        $acceptance.IndexOf($smoke[0]) -ge $acceptance.IndexOf($ui[0]) -or
        -not $smoke[0].Contains("if: success() && inputs.channel == '$channel'") -or
        -not $ui[0].Contains("if: success() && inputs.channel == '$channel'")) {
        throw 'Within each VM, smoke must finish successfully (including cleanup) before independent installed UI starts.'
    }
    if ($channel -eq 'exe') {
        if ($smoke[0] -notmatch 'Run-InstallerTests\.ps1[^\r\n]*-NoBuild' -or $smoke[0] -match 'ExtendedUIReportPath' -or
            $ui[0] -notmatch 'Run-InstallerTests\.ps1[^\r\n]*-NoBuild' -or -not $ui[0].Contains('-ExtendedUIReportPath')) {
            throw 'EXE smoke/UI must install separately and extended UI must use the installed Core.'
        }
    } elseif (-not $smoke[0].Contains('-PackageArtifact PackageArtifacts/msix/package.json -Mode Smoke') -or
              -not $ui[0].Contains('-PackageArtifact PackageArtifacts/msix/package.json -Mode UI')) {
        throw 'MSIX smoke/UI must install the same existing artifact separately, without rebuilding.'
    }
}
foreach ($artifact in @('installer', 'ui-operation', 'msix-smoke', 'msix-ui')) {
    $upload = @($acceptanceSteps | Where-Object { $_.Contains("name: Upload test-results-$artifact`n") -or $_.Contains("name: Upload test-results-$artifact`r`n") })
    $channel = if ($artifact.StartsWith('msix-')) { 'msix' } else { 'exe' }
    if ($upload.Count -ne 1 -or -not $upload[0].Contains("if: always() && inputs.channel == '$channel'") -or
        -not $upload[0].Contains('uses: actions/upload-artifact@')) {
        throw 'Both raw smoke/UI artifacts must be retained even when their channel fails.'
    }
}
foreach ($text in @($workflow, $releaseWorkflow, $acceptanceWorkflow, $action)) {
    foreach ($upload in [regex]::Matches($text, '(?ms)uses: actions/upload-artifact@[^\s]+\r?\n.*?(?=^ {2,4}- |^  [\w-]+:|\z)')) {
        if ($upload.Value -notmatch '(?m)^\s+overwrite:\s*true\s*$') { throw 'Artifact uploads must allow retry.' }
    }
}
$workflowFiles = @(Get-ChildItem "$repositoryRoot/.github/workflows" -File | Where-Object Extension -In @('.yml', '.yaml') | ForEach-Object Name | Sort-Object)
if (($workflowFiles -join ',') -ne 'package-acceptance.yml,pre-merge-check.yml,pre-release.yml') { throw 'Keep two manual entry points and one reusable channel acceptance workflow.' }
if ($releaseWorkflow -match 'force-with-lease|reset --hard') { throw 'Release synchronization must not force-push or discard branch history.' }
if (-not $action.Contains('-RequireCompleteExecution:')) { throw 'Project gates must reject skipped tests.' }
$summary = Get-WorkflowJob 'summarize-tests'
$statusExpression = [regex]::Match($summary, '(?s)const passed = (.*?);').Groups[1].Value
@{ Expression = $statusExpression; Required = @('validate-pr', 'build-solution', 'packages-ready', 'package-exe', 'package-msix') + $specs.Id + @('test-exe-acceptance', 'test-msix-acceptance') } |
    ConvertTo-Json -Compress | & node -e @'
const fs = require('fs');
const f = JSON.parse(fs.readFileSync(0, 'utf8').replace(/^\uFEFF/, ''));
if (!f.Expression) throw Error('Missing PR gate');
const evaluate = new Function('process', `return (${f.Expression});`);
const jobs = Object.fromEntries(f.Required.map(name => [name, {result:'success'}]));
function check(expected, value, ui='Passed', report='success') {
  const env={JOB_RESULTS:JSON.stringify(value),UI_OUTCOME:ui,REPORT_JOB_STATUS:report};
  if (evaluate({env}) !== expected) throw Error(`Incorrect strict PR gate: ${JSON.stringify(env)}`);
}
check(true,jobs);
for(const name of f.Required) for(const result of ['failure','cancelled','skipped']) check(false,{...jobs,[name]:{result}});
for(const ui of ['Skipped','Failed','']) check(false,jobs,ui);
check(false,jobs,'Passed','failure');
'@
if ($LASTEXITCODE -ne 0) { throw 'PR status gate contract failed.' }
if (-not $workflow.Contains('msstore:${{ needs.validate-pr.outputs.msstore_enabled }}') -or
    -not $releaseWorkflow.Contains('msstore:${msstore}]') -or -not $releaseWorkflow.Contains('check?.state')) {
    throw 'Pre-release must verify the checked PR version and channel selection.'
}
Assert-JobNeeds 'sync-versions' @('prepare-metadata', 'package-exe', 'summarize-tests') $releaseWorkflow
Assert-JobNeeds 'draft-release' @('prepare-metadata', 'package-exe', 'summarize-tests', 'sync-versions') $releaseWorkflow
Assert-JobNeeds 'draft-msstore' @('prepare-metadata', 'package-msix', 'summarize-tests', 'sync-versions') $releaseWorkflow
foreach ($id in @('draft-release','draft-msstore')) {
    $job = Get-WorkflowJob $id $releaseWorkflow
    if (-not $job.Contains("needs.summarize-tests.result == 'success'") -or -not $job.Contains('Recheck release selection')) {
        throw 'Both draft channels must wait for strict acceptance and recheck the release selection.'
    }
}
$draft = Get-WorkflowJob 'draft-release' $releaseWorkflow
if (-not $draft.Contains('draft: true') -or -not $draft.Contains('-AllowDraftRetry') -or $draft -match 'dist/.*msix') {
    throw 'The GitHub EXE draft must remain retryable without adding a Store release to the update feed.'
}
if (-not (Get-WorkflowJob 'prepare-metadata' $releaseWorkflow).Contains('-Action Validate') -or
    -not (Get-WorkflowJob 'draft-msstore' $releaseWorkflow).Contains('-Action Draft')) { throw 'Validate Store configuration before building and drafting.' }
$sync = Get-WorkflowJob 'sync-versions' $releaseWorkflow
if ($sync.IndexOf('Assert-ReleaseTagAvailable') -lt 0 -or $sync.IndexOf('Assert-ReleaseTagAvailable') -gt $sync.IndexOf('Sync-ReleaseVersion')) { throw 'Check release conflicts before synchronizing versions.' }
$localRunner = Get-Content "$repositoryRoot/test/Run-Tests.ps1" -Raw
if ($localRunner.IndexOf('& ./test/Run-InstallerTests.ps1') -ge $localRunner.IndexOf('& ./test/Run-UIOperationTests.ps1') -or $localRunner -match 'AllowUIFailure') {
    throw 'Local acceptance remains strict and sequential.'
}
