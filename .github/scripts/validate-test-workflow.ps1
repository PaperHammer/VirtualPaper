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
Assert-JobNeeds 'test-installed-app' ($gates + $specs.Id)
Assert-JobNeeds 'summarize-tests' ($gates + $specs.Id + 'test-installed-app')
$operation = Get-WorkflowJob 'test-installed-app'
$build = Get-WorkflowJob 'build-solution'
$summary = Get-WorkflowJob 'summarize-tests'
if ($operation -match '(?m)^    if:' -or -not $operation.Contains('Get-ProjectTestReports -Root PrerequisiteTestResults') -or
    $operation -notmatch 'Run-InstallerTests\.ps1[^\r\n]*-NoBuild\b[^\r\n]*-RunUIOperationTests\b' -or
    $operation -notmatch '-UIHostExe\s+test/VirtualPaper.UIOperationTest/bin/' -or
    -not $operation.Contains('InnoSetup/setup.iss') -or -not $operation.Contains('name: test-installer-package') -or
    $installerRunner -notmatch 'Run-UIOperationTests\.ps1[^\r\n]*-NoBuild\b[^\r\n]*-Suite\s+Extended\b' -or
    -not $installerRunner.Contains("`$installedCore = Join-Path `$installDir 'VirtualPaper.exe'") -or
    -not $installerRunner.Contains('-CoreExe $installedCore') -or -not $installerRunner.Contains('-UIOperation $uiOperation')) {
    throw 'Installed acceptance must compile ISS, reuse both hosts and run extended UI against the installed Core before uninstall.'
}
if (-not $build.Contains('test/VirtualPaper.UIOperationTest/bin/x64/Release/') -or
    -not $build.Contains('test/VirtualPaper.InstallerTest/bin/Release/') -or
    -not $build.Contains('test/actions/run-project/action.yml') -or -not $build.Contains('test/TestReportSupport.ps1')) {
    throw 'The root-layout build artifact must include the shared action, runners and both test hosts.'
}
if ($workflow -match '(?m)^  (test-installer|test-winui):') {
    throw 'Pre-merge installer and UI acceptance must share one installation job.'
}
if (-not $summary.Contains('./test/Summarize-TestResults.ps1') -or -not $summary.Contains('name: test-summary-') -or
    -not $summary.Contains('UI_OUTCOME: ${{ steps.reports.outputs.ui_outcome }}')) {
    throw 'The final status must require validated raw reports; the summary bundle must use its own prefix.'
}
if ($operation -notmatch 'Run-InstallerTests\.ps1[^\r\n]*-AllowUIFailure\b' -or
    $summary -notmatch 'Summarize-TestResults\.ps1[^\r\n]*-AllowUIFailure\b' -or
    -not $summary.Contains("['Passed', 'Skipped', 'Failed'].includes(process.env.UI_OUTCOME)") -or
    -not $summary.Contains('UI acceptance failed [WARNING]') -or $operation.Contains('continue-on-error: true')) {
    throw 'CI must tolerate only UI-stage failures, retain warning status, and keep installer failures blocking.'
}
if ($build -match 'dotnet test |node .github/scripts/test-') { throw 'Application tests must remain separate from the shared build.' }
foreach ($text in @($workflow, $releaseWorkflow)) {
    if ($text -notmatch '(?m)^  workflow_dispatch:' -or $text -match '(?m)^  (push|pull_request|pull_request_target|workflow_run|schedule|workflow_call):') {
        throw 'Both workflows must be manually dispatched without automatic triggers.'
    }
    if ($text -match '(?m)^    environment:' -or $text -match '(?m)^  approval:' -or -not $text.Contains('pr_number:')) {
        throw 'Manual PR selection replaces the former environment approval layer.'
    }
}
foreach ($text in @($workflow, $releaseWorkflow, $action)) {
    foreach ($upload in [regex]::Matches($text, '(?ms)uses: actions/upload-artifact@v4\r?\n.*?(?=^ {2,4}- |^  [\w-]+:|\z)')) {
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
Assert-JobNeeds 'sync-versions' @('prepare-metadata', 'package-release') $releaseWorkflow
Assert-JobNeeds 'draft-release' @('prepare-metadata', 'package-release', 'sync-versions') $releaseWorkflow
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
if ($releasePackage -notmatch 'Run-InstallerTests\.ps1[^\r\n]*-NoBuild\b[^\r\n]*-RunUIOperationTests\b' -or
    $releasePackage -notmatch 'Summarize-TestResults\.ps1[^\r\n]*-RequireFullAcceptance\b' -or
    -not $releasePackage.Contains('Get-ProjectTestReports -Root TestResults -RequireCompleteExecution') -or
    -not (Get-WorkflowJob 'test-ui-logic' $releaseWorkflow).Contains('node .github/scripts/test-depth-player.cjs') -or
    -not (Get-WorkflowJob 'test-ml' $releaseWorkflow).Contains("include-ml-integration: 'true'")) {
    throw 'Release must run Web checks, ML integration, installed UI and strict reports before synchronizing versions.'
}
if ($releasePackage -notmatch 'Run-InstallerTests\.ps1[^\r\n]*-AllowUIFailure\b' -or
    $releasePackage -notmatch 'Summarize-TestResults\.ps1[^\r\n]*-RequireFullAcceptance\b[^\r\n]*-AllowUIFailure\b' -or
    -not $releaseDraft.Contains('UI_OUTCOME: ${{ needs.package-release.outputs.ui_outcome }}')) {
    throw 'Release must retain non-blocking real UI failures in both acceptance and draft summaries.'
}
if (-not $action.Contains('-RequireCompleteExecution:') -or $releasePackage.Contains('continue-on-error: true')) {
    throw 'Release project and package gates must reject skips and must not ignore failures.'
}
$compileAt = $releasePackage.IndexOf('    - name: Compile installer')
$verifyAt = $releasePackage.IndexOf('    - name: Verify installed release')
$gateAt = $releasePackage.IndexOf('    - name: Validate release checks')
$uploadAt = $releasePackage.IndexOf('    - name: Upload installer package')
if ($compileAt -lt 0 -or $verifyAt -le $compileAt -or $gateAt -le $verifyAt -or $uploadAt -le $gateAt) {
    throw 'Release package order must be compile -> install/startup/UI/uninstall -> strict gate -> upload.'
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
& (Join-Path $repositoryRoot 'test/Test-InstallerProcessSupport.ps1')
& (Join-Path $repositoryRoot 'test/Test-ReportSupport.ps1')
& (Join-Path $repositoryRoot 'test/Test-ReleaseVersion.ps1')
& (Join-Path $repositoryRoot 'test/Test-ReleaseMetadata.ps1')
Write-Host '[OK] Both manual workflows retain non-blocking real UI acceptance; required project/installer gates remain blocking.'
