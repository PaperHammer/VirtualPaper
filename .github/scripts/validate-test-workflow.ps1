$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$solutionPath = Join-Path $repositoryRoot 'src/VirtualPaper.sln'
$workflowPath = Join-Path $repositoryRoot '.github/workflows/pre-publish-branch-ci-check.yml'

$solution = Get-Content -LiteralPath $solutionPath -Raw
$workflow = Get-Content -LiteralPath $workflowPath -Raw
$tests = @(
    @{ Id = 'test-core'; Project = 'VirtualPaper.Core.Test/VirtualPaper.Core.Test.csproj'; Result = 'core-tests.trx' },
    @{ Id = 'test-ui'; Project = 'VirtualPaper.UI.Test/VirtualPaper.UI.Test.csproj'; Result = 'ui-tests.trx' },
    @{ Id = 'test-static-img'; Project = 'StaticImg.Test/StaticImg.Test.csproj'; Result = 'static-img-tests.trx' },
    @{ Id = 'test-web-backdrop'; Project = 'WebBackdrop.Test/WebBackdrop.Test.csproj'; Result = 'web-backdrop-tests.trx' },
    @{ Id = 'test-ui-component'; Project = 'VirtualPaper.UIComponent.Test/VirtualPaper.UIComponent.Test.csproj'; Result = 'ui-component-tests.trx' },
    @{ Id = 'test-ml'; Project = 'VirtualPaper.ML.Test/VirtualPaper.ML.Test.csproj'; Result = 'ml-tests.trx' },
    @{ Id = 'test-shader'; Project = 'VirtualPaper.Shader.Test/VirtualPaper.Shader.Test.csproj'; Result = 'shader-tests.trx' }
)

foreach ($test in $tests) {
    $solutionProject = $test.Project.Replace('/', '\')
    if (-not $solution.Contains($solutionProject)) {
        throw "Test project is missing from solution: $($test.Project)"
    }
    if ($workflow -notmatch "(?m)^  $([regex]::Escape($test.Id)):\s*$") {
        throw "Test job is missing from workflow: $($test.Id)"
    }
    if (-not $workflow.Contains("src/$($test.Project)")) {
        throw "Test project is not invoked by workflow: $($test.Project)"
    }
    if (-not $workflow.Contains($test.Result)) {
        throw "Test result is missing from workflow summary: $($test.Result)"
    }
    $job = [regex]::Match($workflow, "(?ms)^  $([regex]::Escape($test.Id)):\r?\n.*?(?=^  [\w-]+:|\z)").Value
    if (-not $job.Contains('needs: [build, smoke-test]') -or $job -match '(?m)^    if:') {
        throw "$($test.Id) must wait for successful smoke, without dependencies on sibling regular tests."
    }
}

$smokeJob = [regex]::Match($workflow, '(?ms)^  smoke-test:\r?\n.*?(?=^  [\w-]+:|\z)').Value
if ($smokeJob -notmatch '(?m)^    needs: build\s*$' -or $smokeJob -match '(?m)^    if:' -or
    -not $smokeJob.Contains('./test/Run-SmokeTests.ps1')) {
    throw 'Installer smoke must run after build and block all regular tests on failure.'
}
$artifactJob = [regex]::Match($workflow, '(?ms)^  test-release-build:\r?\n.*?(?=^  [\w-]+:|\z)').Value
if (-not $artifactJob.Contains('needs: [build, smoke-test]') -or $artifactJob -match '(?m)^    if:') {
    throw 'Build artifact tests must run in the parallel regular-test phase after smoke.'
}
$buildJob = [regex]::Match($workflow, '(?ms)^  build:\r?\n.*?(?=^  [\w-]+:|\z)').Value
if ($buildJob -match 'dotnet test |node .github/scripts/test-') {
    throw 'Runtime regression tests must not execute in the build prerequisite before smoke.'
}
$regularNeeds = 'needs: [build, smoke-test, test-release-build, ' + (($tests.Id) -join ', ') + ']'
$summaryNeeds = 'needs: [build, smoke-test, test-release-build, ' + (($tests.Id) -join ', ') + ', test-ui-operation]'
if (-not $workflow.Contains($summaryNeeds)) {
    throw 'test-summary must include smoke, artifact checks, seven regular test jobs and WinUI operation tests.'
}
$operationJob = [regex]::Match($workflow, '(?ms)^  test-ui-operation:\r?\n.*?(?=^  [\w-]+:|\z)').Value
if (-not $operationJob.Contains($regularNeeds) -or $operationJob -match '(?m)^    if:') {
    throw 'WinUI operation tests must run only after smoke and all parallel regular test jobs succeed.'
}
$operationProject = 'test/VirtualPaper.UIOperationTest/VirtualPaper.UIOperationTest.csproj'
if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $operationProject)) -or
    -not $solution.Contains($operationProject.Replace('/', '\'))) {
    throw 'WinUI operation test project must exist and be included in the solution.'
}
if (-not $operationJob.Contains('./test/Run-UIOperationTests.ps1') -or
    -not $operationJob.Contains('Verify prerequisite test host health') -or
    -not $workflow.Contains('UI_OPERATION_RESULTS_VALID:-false')) {
    throw 'WinUI operation results must be executed and required by the final status check.'
}
if (-not $workflow.Contains('. ./test/UIOperationTestSupport.ps1') -or
    -not $workflow.Contains('Get-UIOperationReportOutcome $report') -or
    -not $workflow.Contains('Regular tests passed; WinUI skipped [SKIPPED]')) {
    throw 'Environment skips must be validated and reported distinctly from passing WinUI acceptance.'
}
& (Join-Path $repositoryRoot 'test/Test-UIOperationTestSupport.ps1')
if (-not $workflow.Contains('Assert-SmokeReportPassed $report') -or
    -not $workflow.Contains('SMOKE_RESULTS_VALID:-false')) {
    throw 'Final status must require a passing install/launch/uninstall report.'
}
$releaseWorkflow = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../workflows/auto-version-release.yml') -Raw
if (-not $releaseWorkflow.Contains('./test/Run-SmokeTests.ps1')) {
    throw 'Release and pre-publish workflows must share the installer smoke runner.'
}
& (Join-Path $repositoryRoot 'test/Test-SmokeTestSupport.ps1')
if ($workflow -match '(?m)^\s*matrix\s*:') {
    throw 'The regular test workflow must keep explicit jobs and must not use a matrix.'
}
$redundantCoverageCommands = [regex]::Matches($workflow, '--collect:"Code Coverage;Format=Cobertura"').Count
if ($redundantCoverageCommands -ne 0) {
    throw 'The shared runsettings already enables Code Coverage; command-line --collect is redundant.'
}
$coverageSettings = [regex]::Matches($workflow, '--settings src/test\.runsettings').Count
if ($coverageSettings -ne $tests.Count) {
    throw "Expected the shared coverage settings in every regular test job; found $coverageSettings."
}
if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot 'src/test.runsettings'))) {
    throw 'Shared test/coverage settings file is missing: src/test.runsettings'
}
if (-not $workflow.Contains("-notmatch '[\\/]In[\\/]'")) {
    throw 'Coverage summary must exclude the test platform In staging copies.'
}

Write-Host '[OK] Install/launch/uninstall -> parallel regular tests -> real UI; failures block later phases, and UI environment skips stay explicit.'
