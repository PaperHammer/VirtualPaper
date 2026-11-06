$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repositoryRoot 'test/TestReportSupport.ps1')
$workflow = Get-Content -LiteralPath (Join-Path $repositoryRoot '.github/workflows/pre-merge-check.yml') -Raw
$releaseWorkflow = Get-Content -LiteralPath (Join-Path $repositoryRoot '.github/workflows/pre-release.yml') -Raw
$action = Get-Content -LiteralPath (Join-Path $repositoryRoot 'test/actions/run-project/action.yml') -Raw
$solution = Get-Content -LiteralPath (Join-Path $repositoryRoot 'src/VirtualPaper.sln') -Raw
$installerRunner = Get-Content -LiteralPath (Join-Path $repositoryRoot 'test/Run-InstallerTests.ps1') -Raw
$specs = @(Get-ProjectTestSpecs)

function Get-WorkflowJob([string]$Id, [string]$Text = $workflow) {
    $match = [regex]::Match($Text, "(?ms)^  $([regex]::Escape($Id)):\r?\n.*?(?=^  [\w-]+:|\z)")
    if (-not $match.Success) { throw "Missing workflow job: $Id" }
    $match.Value
}
function Assert-JobNeeds([string]$Id, [string[]]$Expected, [string]$Text = $workflow) {
    $job = Get-WorkflowJob $Id $Text
    # Parse the scalar/flow-list needs value and compare sets, independent of ordering or spacing.
    $match = [regex]::Match($job, '(?ms)^    needs:\s*(\[[^\]]*\]|[\w-]+)')
    $actual = @([regex]::Matches($match.Groups[1].Value, '[\w-]+') | ForEach-Object Value)
    if (@(Compare-Object ($actual | Sort-Object -Unique) ($Expected | Sort-Object -Unique)).Count -gt 0 -or
        $actual.Count -ne $Expected.Count) {
        throw "Invalid dependencies for $Id. Expected: $($Expected -join ', '); actual: $($actual -join ', ')."
    }
}
$gates = @('validate-pr', 'build-solution')
Assert-JobNeeds 'build-solution' @('validate-pr')
foreach ($spec in $specs) {
    $project = "src/$($spec.Project)/$($spec.Project).csproj"
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $project)) -or
        -not $solution.Contains("$($spec.Project)\$($spec.Project).csproj")) {
        throw "Test project is missing from source or solution: $project"
    }
    Assert-JobNeeds $spec.Id $gates
    $job = Get-WorkflowJob $spec.Id
    if ($job -match '(?m)^    if:' -or -not $job.Contains('uses: ./test/actions/run-project') -or
        $job -notmatch "(?m)^        job-id:\s*$([regex]::Escape($spec.Id))\s*$") {
        throw "$($spec.Id) must reuse the build through the shared project action before installed acceptance."
    }
}
Assert-JobNeeds 'test-installer-smoke' ($gates + $specs.Id)
Assert-JobNeeds 'test-ui-operation' ($gates + $specs.Id)
Assert-JobNeeds 'summarize-tests' ($gates + $specs.Id + @('test-installer-smoke', 'test-ui-operation'))
$operation = Get-WorkflowJob 'test-installer-smoke'
$uiJob = Get-WorkflowJob 'test-ui-operation'
$build = Get-WorkflowJob 'build-solution'
$summary = Get-WorkflowJob 'summarize-tests'
if ($operation -match '(?m)^    if:' -or -not $operation.Contains('Get-ProjectTestReports -Root PrerequisiteTestResults') -or
    $operation -notmatch 'Run-InstallerTests\.ps1[^\r\n]*-NoBuild\b' -or
    $operation -match 'RunUIOperationTests|AllowUIFailure|continue-on-error: true' -or
    -not $operation.Contains('InnoSetup/setup.iss') -or -not $operation.Contains('name: test-installer-package') -or
    $installerRunner -match 'Run-UIOperationTests|AllowUIFailure|UIRecoveryProcesses') {
    throw 'Required installer smoke must compile ISS, validate normal exits and uninstall independently of extended UI.'
}
if (-not $build.Contains('test/VirtualPaper.UIOperationTest/bin/x64/Release/') -or
    -not $build.Contains('test/VirtualPaper.InstallerTest/bin/Release/') -or
    -not $build.Contains('test/actions/run-project/action.yml') -or -not $build.Contains('test/TestReportSupport.ps1')) {
    throw 'The root-layout build artifact must include the shared action, runners and both test hosts.'
}
if (-not $summary.Contains('./test/Summarize-TestResults.ps1') -or -not $summary.Contains('name: test-summary-') -or
    -not $summary.Contains('UI_OUTCOME: ${{ steps.reports.outputs.ui_outcome }}') -or
    -not $summary.Contains("filter(([name]) => name !== 'test-ui-operation')") -or
    $summary -notmatch 'Summarize-TestResults\.ps1[^\r\n]*-AllowUIFailure\b[^\r\n]*-UIJobResult\b' -or
    -not $summary.Contains('UI acceptance failed [WARNING]')) {
    throw 'PR summary must require smoke/project reports, exclude only the optional UI job and preserve its failures.'
}
if ($build -match 'dotnet test |node .github/scripts/test-') { throw 'Application tests must remain separate from the shared build.' }
# Evaluate the actual PR status expression with failed optional/required jobs.
# This catches a graph that looks separated but still blocks on UI in the final gate.
$statusExpression = [regex]::Match($summary, '(?s)const passed = (.*?);').Groups[1].Value
if (-not $statusExpression) { throw 'PR status expression missing.' }
@{ Expression = $statusExpression; Required = @($gates + $specs.Id + 'test-installer-smoke') } |
    ConvertTo-Json -Depth 5 -Compress | & node -e @'
const fs = require('fs');
const fixture = JSON.parse(fs.readFileSync(0, 'utf8').replace(/^\uFEFF/, ''));
const evaluate = new Function('process', `return (${fixture.Expression});`);
function check(expected, jobs, ui = 'Failed', report = 'success') {
  const env = { JOB_RESULTS: JSON.stringify(jobs), UI_OUTCOME: ui, REPORT_JOB_STATUS: report };
  if (evaluate({ env }) !== expected) throw Error(`Incorrect PR gate: ${JSON.stringify(env)}`);
}
const jobs = Object.fromEntries(fixture.Required.map(name => [name, { result: 'success' }]));
for (const result of ['success', 'failure', 'cancelled', 'skipped']) {
  jobs['test-ui-operation'] = { result };
  for (const ui of ['Passed', 'Failed', 'Skipped']) check(true, jobs, ui);
}
for (const name of fixture.Required) {
  for (const result of ['failure', 'cancelled', 'skipped']) {
    check(false, { ...jobs, [name]: { result } });
  }
}
check(false, jobs, 'Failed', 'failure');
check(false, jobs, '');
console.log('[OK] Actual PR status gate tolerates only independent UI failure and requires smoke/project/summary success.');
'@
if ($LASTEXITCODE -ne 0) { throw 'PR status gate contract failed.' }
foreach ($text in @($workflow, $releaseWorkflow)) {
    if ($text -notmatch '(?m)^  workflow_dispatch:' -or $text -match '(?m)^  (push|pull_request|pull_request_target|workflow_run|schedule|workflow_call):') {
        throw 'Both workflows must be manually dispatched without automatic triggers.'
    }
    if ($text -match '(?m)^    environment:' -or $text -match '(?m)^  approval:' -or -not $text.Contains('pr_number:')) {
        throw 'Manual PR selection replaces the former environment approval layer.'
    }
}
foreach ($text in @($workflow, $releaseWorkflow, $action)) {
    # Match every action ref, so upgrading its version cannot bypass retry checks.
    $uploads = [regex]::Matches($text, '(?ms)uses: actions/upload-artifact@[^\s]+\r?\n.*?(?=^ {2,4}- |^  [\w-]+:|\z)')
    if ($uploads.Count -eq 0) { throw 'Workflow/action artifact uploads are missing from contract validation.' }
    foreach ($upload in $uploads) {
        if ($upload.Value -notmatch '(?m)^\s+overwrite:\s*true\s*$') { throw 'Workflow artifact uploads must allow retries.' }
    }
}
if (-not $workflow.Contains("context: 'pre-merge-check'") -or -not $releaseWorkflow.Contains("check?.state !== 'success'")) {
    throw 'Pre-merge must report the PR head status that Pre-release requires.'
}
$workflowFiles = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot '.github/workflows') -File |
    Where-Object Extension -In @('.yml', '.yaml') | ForEach-Object Name | Sort-Object)
if (($workflowFiles -join ',') -ne 'pre-merge-check.yml,pre-release.yml') { throw 'Only two workflow files should remain.' }
if ($releaseWorkflow -match 'force-with-lease|reset --hard|git merge origin/main') { throw 'Publishing must synchronize only version fields.' }
if ($workflow -match '(?m)^\s*matrix\s*:') { throw 'Keep explicit parallel test jobs.' }
$releasePrepare = Get-WorkflowJob 'prepare-metadata' $releaseWorkflow
$releaseBuild = Get-WorkflowJob 'build-release' $releaseWorkflow
$releasePackage = Get-WorkflowJob 'package-release' $releaseWorkflow
$releaseSync = Get-WorkflowJob 'sync-versions' $releaseWorkflow
$releaseDraft = Get-WorkflowJob 'draft-release' $releaseWorkflow
$releaseSummary = Get-WorkflowJob 'summarize-tests' $releaseWorkflow
Assert-JobNeeds 'build-release' @('prepare-metadata') $releaseWorkflow
foreach ($spec in $specs) {
    Assert-JobNeeds $spec.Id @('prepare-metadata', 'build-release') $releaseWorkflow
    $job = Get-WorkflowJob $spec.Id $releaseWorkflow
    if ($job -match '(?m)^    if:' -or -not $job.Contains('uses: ./test/actions/run-project') -or
        -not $job.Contains('name: build-release-output') -or
        -not $job.Contains("require-complete-execution: 'true'") -or
        $job -notmatch "(?m)^        job-id:\s*$([regex]::Escape($spec.Id))\s*$") {
        throw "Release $($spec.Id) must retest the shared release build through the same project action."
    }
}
Assert-JobNeeds 'package-release' (@('prepare-metadata', 'build-release') + $specs.Id) $releaseWorkflow
Assert-JobNeeds 'test-ui-operation' (@('prepare-metadata', 'build-release') + $specs.Id) $releaseWorkflow
Assert-JobNeeds 'summarize-tests' (@('prepare-metadata', 'build-release') + $specs.Id + @('package-release', 'test-ui-operation')) $releaseWorkflow
Assert-JobNeeds 'sync-versions' @('prepare-metadata', 'package-release', 'summarize-tests') $releaseWorkflow
Assert-JobNeeds 'draft-release' @('prepare-metadata', 'package-release', 'summarize-tests', 'sync-versions') $releaseWorkflow
if ($releaseWorkflow -match '(?m)^  (package-installer|package-plugins|release-summary):' -or
    -not $releasePackage.Contains('name: Build plugin package') -or
    -not $releasePackage.Contains('name: Upload installer package') -or
    -not $releaseDraft.Contains('$env:GITHUB_STEP_SUMMARY')) {
    throw 'Release packaging must share one job, with the summary in the draft job.'
}
if (-not $releaseBuild.Contains('          src/') -or
    -not $releaseBuild.Contains('test/VirtualPaper.UIOperationTest/bin/x64/Release/') -or
    -not $releaseBuild.Contains('test/VirtualPaper.InstallerTest/bin/Release/') -or
    $releasePackage -notmatch 'name: build-release-output\r?\n        path: \.\s*\r?\n' -or
    $releaseBuild.Contains('dotnet test ')) {
    throw 'Release tests and packaging must reuse one root-layout build with both test hosts.'
}
if ($releasePackage -notmatch 'Run-InstallerTests\.ps1[^\r\n]*-NoBuild\b' -or
    $releasePackage -match 'RunUIOperationTests|AllowUIFailure|continue-on-error: true' -or
    $releaseSummary -notmatch 'Summarize-TestResults\.ps1[^\r\n]*-RequireFullAcceptance\b[^\r\n]*-AllowUIFailure\b[^\r\n]*-UIJobResult\b' -or
    -not $releasePackage.Contains('Get-ProjectTestReports -Root TestResults -RequireCompleteExecution') -or
    -not (Get-WorkflowJob 'test-ui-logic' $releaseWorkflow).Contains('node .github/scripts/test-depth-player.cjs') -or
    -not (Get-WorkflowJob 'test-ml' $releaseWorkflow).Contains("include-ml-integration: 'true'") -or
    -not $releaseDraft.Contains('UI_OUTCOME: ${{ needs.summarize-tests.outputs.ui_outcome }}') -or
    -not $releaseDraft.Contains("needs.summarize-tests.result == 'success'")) {
    throw 'Release must require project/smoke success before syncing versions or drafting, with independent UI warnings.'
}
if (-not $action.Contains('-RequireCompleteExecution:')) { throw 'Release project gates must reject skips.' }
foreach ($text in @($workflow, $releaseWorkflow)) {
    $ui = Get-WorkflowJob 'test-ui-operation' $text
    if ($ui -notmatch 'Run-UIOperationTests\.ps1[^\r\n]*-NoBuild\b[^\r\n]*-Suite\s+Extended\b' -or
        $ui -notmatch '(?m)^    continue-on-error: true' -or
        -not $ui.Contains('result: ${{ steps.ui-tests.outcome }}') -or
        $ui -match 'Run-InstallerTests|InstallerPath|CoreExe') {
        throw 'Optional UI must independently run the Release build with the real Core, retaining runner outcome and artifacts.'
    }
    foreach ($job in [regex]::Matches($text, '(?ms)^  [\w-]+:\r?\n.*?(?=^  [\w-]+:|\z)')) {
        if ($job.Value -match 'continue-on-error: true' -and -not $job.Value.StartsWith('  test-ui-operation:')) {
            throw 'Only the independent real UI job may ignore failures.'
        }
    }
}
$compileAt = $releasePackage.IndexOf('    - name: Compile installer')
$verifyAt = $releasePackage.IndexOf('    - name: Run installer smoke')
$uploadAt = $releasePackage.IndexOf('    - name: Upload installer package')
if ($compileAt -lt 0 -or $verifyAt -le $compileAt -or $uploadAt -le $verifyAt) {
    throw 'Release package order must be compile -> mandatory installer smoke -> upload.'
}
$localRunner = Get-Content -LiteralPath (Join-Path $repositoryRoot 'test/Run-Tests.ps1') -Raw
if ($localRunner.IndexOf('& ./test/Run-InstallerTests.ps1') -ge $localRunner.IndexOf('& ./test/Run-UIOperationTests.ps1') -or
    $localRunner -match 'RunUIOperationTests|AllowUIFailure' -or
    $localRunner -notmatch 'Run-UIOperationTests\.ps1[^\r\n]*-NoBuild\b[^\r\n]*-Suite\s+Extended\b') {
    throw 'Local acceptance must finish strict smoke/uninstall before running independent strict UI acceptance.'
}
if (-not $releasePrepare.Contains('Assert-ReleaseTagAvailable') -or
    -not (Get-WorkflowJob 'build-release' $releaseWorkflow).Contains('needs: prepare-metadata') -or
    $releaseSync.IndexOf('Assert-ReleaseTagAvailable') -lt 0 -or
    $releaseSync.IndexOf('Assert-ReleaseTagAvailable') -gt $releaseSync.IndexOf('Sync-ReleaseVersion')) {
    throw 'Release conflicts must be checked before building and rechecked before version synchronization.'
}
$manifestDownload = [regex]::Match($releaseDraft, '(?ms)^    - name: Download component manifest\r?\n.*?(?=^    - name:|\z)').Value
if (-not $manifestDownload.Contains('name: app-comp-manifest') -or $manifestDownload -match '(?m)^      if:' -or
    -not $releaseDraft.Contains('-AllowDraftRetry')) {
    throw 'Every release must attach the manifest and allow only matching draft retries.'
}
foreach ($step in [regex]::Matches($releaseWorkflow, '(?ms)^    - name:.*?(?=^    - name:|^  [\w-]+:|\z)')) {
    if ($step.Value -match 'Get-ReleaseHistory|Get-ReleaseBaseline|Assert-ReleaseTagAvailable') {
        if (-not $step.Value.Contains('GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}')) { throw 'Release API steps require authentication.' }
    }
}

& (Join-Path $repositoryRoot 'test/Test-UIOperationTestSupport.ps1')
& (Join-Path $repositoryRoot 'test/Test-InstallerTestSupport.ps1')
& (Join-Path $repositoryRoot 'test/Test-BuiltInWallpaperTestSupport.ps1')
& (Join-Path $repositoryRoot 'test/Test-InstallerProcessSupport.ps1')
& (Join-Path $repositoryRoot 'test/Test-ReportSupport.ps1')
& (Join-Path $repositoryRoot 'test/Test-ReleaseVersion.ps1')
& (Join-Path $repositoryRoot 'test/Test-ReleaseMetadata.ps1')
Write-Host '[OK] Both workflows require independent installer smoke; only standalone real UI acceptance is non-blocking.'
