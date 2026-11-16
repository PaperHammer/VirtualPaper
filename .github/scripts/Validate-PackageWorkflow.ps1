# Called by validate-test-workflow.ps1 after loading its workflow/job helpers.
foreach ($text in @($workflow, $releaseWorkflow, $acceptanceWorkflow)) {
    if ($text -notmatch '(?m)^defaults:\r?\n  run:\r?\n    shell: pwsh\s*$') {
        throw 'All workflow PowerShell run blocks, including Ubuntu gates/summaries, require the pwsh default.'
    }
}
$sharedNamePairs = @{ 'build-solution' = 'build-release' }
foreach ($id in @('package-exe', 'package-msix', 'packages-ready', 'tests-passed', 'test-exe-acceptance', 'test-msix-acceptance', 'summarize-tests') + $specs.Id) {
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
    if ($text -match '(?m)^    environment:|continue-on-error:|UIJobResult' -or -not $text.Contains('pr_number:')) {
        throw 'Package, project and channel jobs must remain required; only extended UI steps may tolerate failures.'
    }
    $isRelease = $text -ceq $releaseWorkflow
    $metadata = if ($isRelease) { 'prepare-metadata' } else { 'validate-pr' }
    $buildId = if ($isRelease) { 'build-release' } else { 'build-solution' }
    $artifactName = if ($isRelease) { 'build-release-output' } else { 'build-output' }
    $gates = @($metadata, $buildId)
    Assert-JobNeeds $buildId @($metadata) $text
    foreach ($id in @('package-exe', 'package-msix')) { Assert-JobNeeds $id $gates $text }
    $msix = Get-WorkflowJob 'package-msix' $text
    $cache = [regex]::Match($msix, '(?ms)^    - name: Cache NuGet packages\r?\n.*?(?=^    - name:|\z)')
    $checkoutIndex = $msix.IndexOf('    - name: Check out source', [StringComparison]::Ordinal)
    $downloadIndex = $msix.IndexOf('    - name: Download build output', [StringComparison]::Ordinal)
    if (-not $cache.Success -or -not $cache.Value.Contains('uses: actions/cache@') -or
        -not $cache.Value.Contains('path: ~/.nuget/packages') -or -not $cache.Value.Contains('key: nuget-msix-${{ runner.os }}-') -or
        -not $cache.Value.Contains("hashFiles('src/**/*.csproj'") -or -not $cache.Value.Contains("'global.json'") -or
        -not $cache.Value.Contains("'.github/scripts/Build-Msix.ps1'") -or
        -not $cache.Value.Contains('nuget-${{ runner.os }}-') -or
        $checkoutIndex -lt 0 -or $cache.Index -lt $checkoutIndex -or $downloadIndex -lt $cache.Index) {
        throw 'MSIX must restore its source-keyed NuGet cache after checkout and before downloading generated build output.'
    }
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
        Assert-JobNeeds $spec.Id $gates $text
        $job = Get-WorkflowJob $spec.Id $text
        if ($job -match '(?m)^    if:' -or -not $job.Contains('uses: ./test/actions/run-project') -or
            -not $job.Contains("name: $artifactName") -or -not $job.Contains("require-complete-execution: 'true'")) {
            throw 'Project jobs must reuse the fixed build in parallel with packaging and reject skipped tests.'
        }
    }
    Assert-JobNeeds 'tests-passed' $specs.Id $text
    $testGate = Get-WorkflowJob 'tests-passed' $text
    $gateRun = [regex]::Match($testGate, '(?ms)^      run: \|\r?\n(.*)\z')
    if (-not $testGate.Contains('if: always() && !cancelled()') -or
        -not $testGate.Contains('JOB_RESULTS: ${{ toJSON(needs) }}') -or -not $gateRun.Success) {
        throw 'Tests Passed must aggregate all project outcomes even when a prerequisite failed or skipped.'
    }
    $gateScript = [scriptblock]::Create([regex]::Replace($gateRun.Groups[1].Value, '(?m)^        ', ''))
    $previousJobResults = $env:JOB_RESULTS
    $fixtureJobs = @{}
    foreach ($spec in $specs) { $fixtureJobs[$spec.Id] = @{ result = 'success' } }
    try {
        $env:JOB_RESULTS = $fixtureJobs | ConvertTo-Json -Depth 4 -Compress
        & $gateScript
        foreach ($spec in $specs) {
            foreach ($result in @('failure', 'cancelled', 'skipped', $null)) {
                $fixtureJobs[$spec.Id].result = $result
                $env:JOB_RESULTS = $fixtureJobs | ConvertTo-Json -Depth 4 -Compress
                $rejected = $false
                try { & $gateScript } catch { $rejected = $true }
                if (-not $rejected) { throw "Tests Passed accepted a non-passing project: $($spec.Id) / $result" }
            }
            $fixtureJobs.Remove($spec.Id)
            $env:JOB_RESULTS = $fixtureJobs | ConvertTo-Json -Depth 4 -Compress
            $rejected = $false
            try { & $gateScript } catch { $rejected = $true }
            if (-not $rejected) { throw "Tests Passed accepted missing project results: $($spec.Id)" }
            $fixtureJobs[$spec.Id] = @{ result = 'success' }
        }
    } finally { $env:JOB_RESULTS = $previousJobResults }
    $tests = @('test-exe-acceptance', 'test-msix-acceptance')
    foreach ($channel in @('exe', 'msix')) {
        $id = "test-$channel-acceptance"
        Assert-JobNeeds $id ($gates + @('packages-ready', 'tests-passed')) $text
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
    Assert-JobNeeds 'summarize-tests' ($gates + @('package-exe', 'package-msix', 'packages-ready', 'tests-passed') + $specs.Id + $tests) $text
    if (-not $summary.Contains('./test/Summarize-PipelineTests.ps1') -or -not $summary.Contains('${{ toJSON(needs) }}') -or
        -not $summary.Contains('pattern: test-results-*') -or -not $summary.Contains('-AllowUIFailure')) {
        throw 'Summary must require raw reports and package identities while allowing reported extended UI failures.'
    }
    foreach ($id in @('package-msix', 'test-msix-acceptance')) {
        $job = Get-WorkflowJob $id $text
        if ($isRelease) {
            if (-not $job.Contains("if: needs.prepare-metadata.outputs.msstore_enabled == 'true'")) { throw 'Release MSIX jobs must be selected by the frozen PR label.' }
        } elseif ($job -match '(?m)^    if:') { throw 'Pre-check must always validate both package channels.' }
    }
}
if ($acceptanceWorkflow -notmatch '(?m)^  workflow_call:' -or
    $acceptanceWorkflow -match '(?m)^  (push|pull_request|pull_request_target|workflow_run|workflow_dispatch|schedule):' -or
    $acceptanceWorkflow -match 'AllowUIFailure|UIJobResult|secrets:|New-CIMsixPackage|Build-Msix|Run-ProjectTests|dotnet test ') {
    throw 'Installed acceptance must be a required reusable workflow that only consumes existing packages/builds.'
}
$jobSection = ($acceptanceWorkflow -split '(?m)^jobs:\r?\n', 2)[1]
$jobIds = @([regex]::Matches($jobSection, '(?m)^  ([\w-]+):') | ForEach-Object { $_.Groups[1].Value })
if ((($jobIds | Sort-Object) -join ',') -cne 'smoke,ui') {
    throw 'Each channel requires separate smoke and UI jobs.'
}
$stepsByJob = @{}
foreach ($jobId in @('smoke', 'ui')) {
    $acceptance = Get-WorkflowJob $jobId $acceptanceWorkflow
    if ($acceptance -match '(?m)^    (needs|strategy):' -or
        $acceptance -notmatch '(?m)^    runs-on: windows-latest\s*$' -or
        -not $acceptance.Contains("`$env:RUNNER_ENVIRONMENT -cne 'github-hosted'") -or
        -not $acceptance.Contains('ref: ${{ inputs.source-sha }}') -or
        -not $acceptance.Contains('name: ${{ inputs.build-artifact }}') -or
        -not $acceptance.Contains('name: package-${{ inputs.channel }}') -or
        -not $acceptance.Contains('Get-PackageArtifact "PackageArtifacts/$env:CHANNEL/package.json" $env:CHANNEL $env:SOURCE_SHA')) {
        throw 'Smoke and UI must run independently on fresh hosted VMs and validate the same frozen source/build/package.'
    }
    $stepsByJob[$jobId] = @([regex]::Matches($acceptance, '(?ms)^    - name: [^\r\n]+.*?(?=^    - name: |\z)') | ForEach-Object Value)
}
$smokeSteps = $stepsByJob.smoke; $uiSteps = $stepsByJob.ui
$acceptanceSteps = @($smokeSteps) + @($uiSteps)
if ([regex]::Matches($acceptanceWorkflow, '(?m)^    continue-on-error: true\s*$').Count -ne 1 -or
    -not (Get-WorkflowJob 'ui' $acceptanceWorkflow).Contains('    continue-on-error: true') -or
    (Get-WorkflowJob 'smoke' $acceptanceWorkflow) -match 'continue-on-error:' -or
    -not $acceptanceWorkflow.Contains('value: ${{ jobs.ui.outputs.ui_run_result }}') -or
    -not $acceptanceWorkflow.Contains('value: ${{ jobs.ui.outputs.ui_upload_result }}') -or
    -not (Get-WorkflowJob 'ui' $acceptanceWorkflow).Contains("ui_run_result: `${{ inputs.channel == 'exe' && steps.exe-ui.outcome || steps.msix-ui.outcome }}") -or
    -not (Get-WorkflowJob 'ui' $acceptanceWorkflow).Contains("ui_upload_result: `${{ inputs.channel == 'exe' && steps.upload-exe-ui.outcome || steps.upload-msix-ui.outcome }}")) {
    throw 'Expose actual UI run/upload outcomes; only the UI job may tolerate failure while retaining a failed check.'
}
foreach ($step in $acceptanceSteps) {
    if ($step -match 'continue-on-error:') {
        throw 'Step failures must remain visible; only the UI job may tolerate failure, with required uploads checked by the summary.'
    }
}
# Execute each job's actual first-step guard without touching installations or a desktop.
$guardEnvironment = @{}
foreach ($name in @('CHANNEL', 'SOURCE_SHA', 'BUILD_ARTIFACT', 'RUNNER_ENVIRONMENT')) {
    $guardEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}
try {
    foreach ($jobId in @('smoke', 'ui')) {
        $guardMatch = [regex]::Match($stepsByJob[$jobId][0], '(?ms)^      run: \|\r?\n(.*)\z')
        if (-not $guardMatch.Success) { throw 'The runner/input guard must precede every job installation.' }
        $guard = [scriptblock]::Create([regex]::Replace($guardMatch.Groups[1].Value, '(?m)^        ', ''))
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
    }
} finally {
    foreach ($name in $guardEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $guardEnvironment[$name]) }
}
Write-Host '[OK] Both independent job guards accept both channels/builds and reject self-hosted or invalid inputs.'
foreach ($channel in @('exe', 'msix')) {
    $smoke = @($smokeSteps | Where-Object { $_ -match "(?m)^      id: $channel-smoke\s*$" })
    $ui = @($uiSteps | Where-Object { $_ -match "(?m)^      id: $channel-ui\s*$" })
    if ($smoke.Count -ne 1 -or $ui.Count -ne 1 -or
        @($smokeSteps | Where-Object { $_ -match '(?m)^      id: (exe|msix)-ui\s*$' }).Count -or
        @($uiSteps | Where-Object { $_ -match '(?m)^      id: (exe|msix)-smoke\s*$' }).Count -or
        -not $smoke[0].Contains("if: success() && inputs.channel == '$channel'") -or
        -not $ui[0].Contains("if: success() && inputs.channel == '$channel'") -or
        -not $ui[0].Contains("name: $($channel.ToUpperInvariant()) UI Operation Acceptance")) {
        throw 'Smoke and UI must stay in separate parallel jobs, with clear channel-specific UI operation acceptance names.'
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
    $ownerSteps = if ($artifact -in @('installer', 'msix-smoke')) { $smokeSteps } else { $uiSteps }
    $channel = if ($artifact.StartsWith('msix-')) { 'msix' } else { 'exe' }
    if ($upload.Count -ne 1 -or -not $upload[0].Contains("if: always() && inputs.channel == '$channel'") -or
        -not $upload[0].Contains('uses: actions/upload-artifact@') -or -not $upload[0].Contains('if-no-files-found: error') -or
        ($artifact -in @('ui-operation', 'msix-ui') -and -not $upload[0].Contains("id: upload-$channel-ui")) -or
        $ownerSteps -cnotcontains $upload[0]) {
        throw 'Both raw smoke/UI uploads remain required, including when the extended UI run fails.'
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
@{ Expression = $statusExpression; Required = @('validate-pr', 'build-solution', 'packages-ready', 'tests-passed', 'package-exe', 'package-msix') + $specs.Id + @('test-exe-acceptance', 'test-msix-acceptance') } |
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
for (const name of ['test-exe-acceptance', 'test-msix-acceptance']) {
  const failed = {result:'failure', outputs:{ui_run_result:'failure',ui_upload_result:'success'}};
  check(true,{...jobs,[name]:failed},'Failed');
  for (const ui_upload_result of ['failure','cancelled','skipped',undefined]) {
    check(false,{...jobs,[name]:{...failed,outputs:{...failed.outputs,ui_upload_result}}},'Failed');
  }
  check(false,{...jobs,[name]:failed},'Failed','failure');
}
for(const ui of ['Skipped','Failed']) check(true,jobs,ui);
check(false,jobs,'');
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
# Evaluate the actual channel-selection and draft conditions, rather than relying
# only on matching text or treating a skipped optional channel as a failed draft.
function Get-ReleaseCondition([string] $Text, [int] $Indent) {
    $prefix = ' ' * $Indent
    $block = [regex]::Match($Text, "(?m)^${prefix}if: \|\r?\n((?:${prefix}  [^\r\n]+\r?\n)+)")
    if ($block.Success) { return $block.Groups[1].Value.Trim() }
    $inline = [regex]::Match($Text, "(?m)^${prefix}if: ([^\r\n]+)")
    if (-not $inline.Success) { throw 'Missing release channel condition.' }
    return $inline.Groups[1].Value.Trim()
}
$prepare = Get-WorkflowJob 'prepare-metadata' $releaseWorkflow
$storeConfig = [regex]::Match($prepare, '(?ms)^    - name: Validate Partner Center configuration\r?\n.*?(?=^    - name:|\z)').Value
@{
    Selection = [regex]::Match($prepare, 'const msstore = ([^;]+);').Groups[1].Value
    Configuration = Get-ReleaseCondition $storeConfig 6
    Package = Get-ReleaseCondition (Get-WorkflowJob 'package-msix' $releaseWorkflow) 4
    Acceptance = Get-ReleaseCondition (Get-WorkflowJob 'test-msix-acceptance' $releaseWorkflow) 4
    Sync = Get-ReleaseCondition (Get-WorkflowJob 'sync-versions' $releaseWorkflow) 4
    GitHubDraft = Get-ReleaseCondition (Get-WorkflowJob 'draft-release' $releaseWorkflow) 4
    StoreDraft = Get-ReleaseCondition (Get-WorkflowJob 'draft-msstore' $releaseWorkflow) 4
} | ConvertTo-Json -Compress | & node -e @'
const fs = require('fs');
const conditions = JSON.parse(fs.readFileSync(0, 'utf8').replace(/^\uFEFF/, ''));
const selected = new Function('pr', `return (${conditions.Selection});`);
for (const [labels, expected] of [
  [[], false], [['version:build'], false], [['channel:msstore-preview'], false],
  [['channel:msstore'], true], [['version:build', 'channel:msstore'], true]
]) {
  if (selected({labels: labels.map(name => ({name}))}) !== expected) throw Error('Select Store only by the exact PR label.');
}
function fixture(enabled=true, plugin=false) {
  return {
    'prepare-metadata': {result:'success', outputs:{msstore_enabled:String(enabled), has_plugin_update:String(plugin)}},
    'package-exe': {result:'success'}, 'package-msix': {result: enabled ? 'success' : 'skipped'},
    'summarize-tests': {result:'success'}, 'sync-versions': {result:plugin ? 'skipped' : 'success'},
    'draft-release': {result:'success'}, 'draft-msstore': {result:enabled ? 'success' : 'skipped'}
  };
}
function check(name, expected, needs, cancelled=false) {
  const steps = {'release-pr': {outputs:needs['prepare-metadata'].outputs}};
  const expression=conditions[name].replace(/\b(needs|steps)\.([\w-]+)/g, (_, owner, key) => `${owner}[${JSON.stringify(key)}]`);
  const actual = new Function('needs', 'steps', 'cancelled', `return (${expression});`)(needs, steps, () => cancelled);
  if (actual !== expected) throw Error(`Incorrect release condition: ${name} / ${JSON.stringify(needs)}`);
}
for (const enabled of [false,true]) for (const plugin of [false,true]) {
  const needs=fixture(enabled,plugin);
  for (const stage of ['Configuration','Package','Acceptance','StoreDraft']) check(stage,enabled,needs);
  check('GitHubDraft',true,needs);
  check('Sync',!plugin,needs);
  needs['test-exe-acceptance']={result:'failure',outputs:{ui_run_result:'failure',ui_upload_result:'success'}};
  check('Sync',!plugin,needs);
  needs['draft-msstore'].result='failure'; check('GitHubDraft',true,needs);
  needs['draft-release'].result='failure'; check('StoreDraft',enabled,needs);
}
for (const [stage, packageId] of [['Sync','package-exe'],['GitHubDraft','package-exe'],['StoreDraft','package-msix']]) {
  const required = ['prepare-metadata',packageId,'summarize-tests'];
  if (stage !== 'Sync') required.push('sync-versions');
  for (const prerequisite of required) {
    for (const result of ['failure','cancelled','skipped']) {
      const needs=fixture(); needs[prerequisite].result=result; check(stage,false,needs);
    }
  }
  check(stage,false,fixture(),true);
}
'@
if ($LASTEXITCODE -ne 0) { throw 'Release channel selection or independent draft conditions failed.' }
Write-Host '[OK] Store-only stages follow the exact PR label; both draft channels run independently after their required gates.'
$sync = Get-WorkflowJob 'sync-versions' $releaseWorkflow
if ($sync.IndexOf('Assert-ReleaseTagAvailable') -lt 0 -or $sync.IndexOf('Assert-ReleaseTagAvailable') -gt $sync.IndexOf('Sync-ReleaseVersion')) { throw 'Check release conflicts before synchronizing versions.' }
$localRunner = Get-Content "$repositoryRoot/test/Run-Tests.ps1" -Raw
if ($localRunner.IndexOf('& ./test/Run-InstallerTests.ps1') -ge $localRunner.IndexOf('& ./test/Run-UIOperationTests.ps1') -or $localRunner -match 'AllowUIFailure') {
    throw 'Local acceptance remains strict and sequential.'
}
