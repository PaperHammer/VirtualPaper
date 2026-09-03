$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
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
}

$summaryNeeds = 'needs: [build, ' + (($tests.Id) -join ', ') + ']'
if (-not $workflow.Contains($summaryNeeds)) {
    throw "test-summary dependencies do not match the seven regular test jobs."
}
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

Write-Host "[OK] Seven test projects and their shared coverage settings are present in the solution, workflow jobs, and result summary."
