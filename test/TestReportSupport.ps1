function Get-ProjectTestSpecs {
    @(
        @{ Id = 'test-artifacts'; Project = 'VirtualPaper.ReleaseBuildData.Test'; Directory = 'ReleaseBuild'; Report = 'release-build-tests.trx'; Artifact = 'test-results-release-build'; Coverage = $false },
        @{ Id = 'test-core'; Project = 'VirtualPaper.Core.Test'; Directory = 'Core'; Report = 'core-tests.trx'; Artifact = 'test-results-core'; Coverage = $true },
        @{ Id = 'test-ui-logic'; Project = 'VirtualPaper.UI.Test'; Directory = 'UI'; Report = 'ui-tests.trx'; Artifact = 'test-results-ui'; Coverage = $true },
        @{ Id = 'test-images'; Project = 'StaticImg.Test'; Directory = 'StaticImg'; Report = 'static-img-tests.trx'; Artifact = 'test-results-static-img'; Coverage = $true },
        @{ Id = 'test-web-backdrop'; Project = 'WebBackdrop.Test'; Directory = 'WebBackdrop'; Report = 'web-backdrop-tests.trx'; Artifact = 'test-results-web-backdrop'; Coverage = $true },
        @{ Id = 'test-ui-components'; Project = 'VirtualPaper.UIComponent.Test'; Directory = 'UIComponent'; Report = 'ui-component-tests.trx'; Artifact = 'test-results-ui-component'; Coverage = $true },
        @{ Id = 'test-ml'; Project = 'VirtualPaper.ML.Test'; Directory = 'ML'; Report = 'ml-tests.trx'; Artifact = 'test-results-ml'; Coverage = $true },
        @{ Id = 'test-shader'; Project = 'VirtualPaper.Shader.Test'; Directory = 'Shader'; Report = 'shader-tests.trx'; Artifact = 'test-results-shader'; Coverage = $true }
    ) | ForEach-Object { [pscustomobject]$_ }
}

function Assert-TrxReportPassed {
    param([Parameter(Mandatory)][string]$Path, [switch]$RequireCompleteExecution)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Test report is missing: $Path" }
    [xml]$document = Get-Content -LiteralPath $Path -Raw
    $summary = $document.TestRun.ResultSummary
    if ($null -eq $summary -or $null -eq $summary.Counters) { throw "Invalid TRX summary: $Path" }
    $counts = @{}
    foreach ($field in @('total', 'executed', 'passed', 'failed', 'aborted')) {
        $count = 0L
        if (-not [long]::TryParse([string]$summary.Counters.$field, [ref]$count) -or $count -lt 0) {
            throw "Invalid TRX counter '$field': $Path"
        }
        $counts[$field] = $count
    }
    foreach ($field in @('error', 'timeout', 'passedButRunAborted', 'notRunnable', 'disconnected')) {
        if ($summary.Counters.HasAttribute($field) -and [string]$summary.Counters.$field -ne '0') {
            throw "TRX counter '$field' is not zero: $Path"
        }
    }
    $output = [string]$summary.Output.StdOut + "`n" + [string]$summary.Output.StdErr
    if ($counts.total -lt 1 -or $counts.executed -lt 1 -or $counts.passed -lt 1 -or
        $counts.executed -gt $counts.total -or $counts.passed -ne $counts.executed -or
        $counts.failed -gt 0 -or $counts.aborted -gt 0 -or
        $summary.outcome -notin @('Completed', 'Passed') -or
        $output -match '(?im)^\s*Unhandled exception\.') {
        throw "Test report is incomplete, failed or contains an unhandled exception: $Path"
    }
    if ($RequireCompleteExecution -and $counts.total -ne $counts.executed) {
        throw "Release project tests must execute without skips: $Path"
    }
    [pscustomobject]@{ Path = $Path; Total = $counts.total; Executed = $counts.executed; Passed = $counts.passed }
}

function Get-ProjectTestReports {
    param([Parameter(Mandatory)][string]$Root, [switch]$RequireCompleteExecution)
    # Read exact raw-report paths. Combined bundles and unrelated artifacts are never inputs.
    foreach ($spec in Get-ProjectTestSpecs) {
        Assert-TrxReportPassed (Join-Path $Root "$($spec.Artifact)/$($spec.Report)") -RequireCompleteExecution:$RequireCompleteExecution
    }
}

function Get-TestCoverageRows {
    param([Parameter(Mandatory)][string]$Root)
    foreach ($spec in Get-ProjectTestSpecs | Where-Object Coverage) {
        $files = @(Get-ChildItem -LiteralPath (Join-Path $Root $spec.Artifact) -Filter '*.cobertura.xml' -Recurse |
            Where-Object { $_.FullName -notmatch '[\\/]In[\\/]' })
        if ($files.Count -ne 1) { throw "Expected one coverage report for $($spec.Id), found $($files.Count)." }
        [xml]$document = Get-Content -LiteralPath $files[0].FullName -Raw
        $covered = 0L; $valid = 0L
        if (-not [long]::TryParse([string]$document.coverage.'lines-covered', [ref]$covered) -or
            -not [long]::TryParse([string]$document.coverage.'lines-valid', [ref]$valid) -or
            $covered -lt 0 -or $valid -lt 0 -or $covered -gt $valid) {
            throw "Invalid coverage counters: $($files[0].FullName)"
        }
        [pscustomobject]@{ Project = $spec.Project; Covered = $covered; Valid = $valid }
    }
}
