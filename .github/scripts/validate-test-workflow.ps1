$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
& (Join-Path $repositoryRoot 'test/Test-LocalizationResources.ps1')
. (Join-Path $repositoryRoot 'test/TestReportSupport.ps1')
$workflow = Get-Content -LiteralPath (Join-Path $repositoryRoot '.github/workflows/pre-merge-check.yml') -Raw
$releaseWorkflow = Get-Content -LiteralPath (Join-Path $repositoryRoot '.github/workflows/pre-release.yml') -Raw
$acceptanceWorkflow = Get-Content -LiteralPath (Join-Path $repositoryRoot '.github/workflows/package-acceptance.yml') -Raw
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
# The shared package validator owns the new dual-channel DAG and strict acceptance gates.
. (Join-Path $PSScriptRoot 'Validate-PackageWorkflow.ps1')
& (Join-Path $repositoryRoot 'test/Test-PackagePipeline.ps1')

# Retain branch-independent release and local-runner checks from the existing workflow.
$releasePrepare = Get-WorkflowJob 'prepare-metadata' $releaseWorkflow
$releaseDraft = Get-WorkflowJob 'draft-release' $releaseWorkflow
if (-not $releasePrepare.Contains('Assert-ReleaseTagAvailable') -or
    -not (Get-WorkflowJob 'test-ui-logic' $releaseWorkflow).Contains('node .github/scripts/test-depth-player.cjs') -or
    -not (Get-WorkflowJob 'test-ml' $releaseWorkflow).Contains("include-ml-integration: 'true'")) {
    throw 'Release must check conflicts before building and retain depth scripts and real model integration.'
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
$localRunner = Get-Content -LiteralPath (Join-Path $repositoryRoot 'test/Run-Tests.ps1') -Raw
if ($localRunner -match 'RunUIOperationTests|AllowUIFailure' -or
    $localRunner -notmatch 'Run-UIOperationTests\.ps1[^\r\n]*-NoBuild\b[^\r\n]*-Suite\s+Extended\b') {
    throw 'Local acceptance must retain independent strict extended UI after installed smoke.'
}

& (Join-Path $repositoryRoot 'test/Test-UIOperationTestSupport.ps1')
& (Join-Path $repositoryRoot 'test/Test-MsixTestSupport.ps1')
& (Join-Path $repositoryRoot 'test/Test-InstallerTestSupport.ps1')
& (Join-Path $repositoryRoot 'test/Test-BuiltInWallpaperTestSupport.ps1')
& (Join-Path $repositoryRoot 'test/Test-InstallerProcessSupport.ps1')
& (Join-Path $repositoryRoot 'test/Test-ReportSupport.ps1')
& (Join-Path $repositoryRoot 'test/Test-ReleaseVersion.ps1')
& (Join-Path $repositoryRoot 'test/Test-ReleaseMetadata.ps1')
Write-Host '[OK] Both workflows require packages, shared project tests, isolated channel workflows with sequential smoke/UI, and strict draft gates.'
