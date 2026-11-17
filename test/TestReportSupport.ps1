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
    $modules = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::OrdinalIgnoreCase)
    $owners = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
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
        $lineCount = 0; $hasHit = $false
        foreach ($package in $document.SelectNodes('/coverage/packages/package')) {
            $packageName = $package.GetAttribute('name')
            if ([string]::IsNullOrWhiteSpace($packageName)) { throw "Coverage module name is missing: $($files[0].FullName)" }
            foreach ($class in $package.SelectNodes('classes/class')) {
                $source = $class.GetAttribute('filename').Replace('\', '/') -replace '^(\./)+', ''
                if ([string]::IsNullOrWhiteSpace($source)) { throw "Coverage source path is missing: $packageName" }
                if ($source -notmatch '^(/|[a-zA-Z]:/|(?i:src|test)/)') {
                    $sourceRoot = $document.SelectSingleNode('/coverage/sources/source')
                    if ($sourceRoot -and $sourceRoot.InnerText) { $source = $sourceRoot.InnerText.Replace('\', '/') + '/' + $source }
                }
                # Report roots differ between local/CI checkouts. Keep the authored
                # source path, then normalize separators and dot segments before merging.
                $source = $source -replace '(?i)^.*?/(src|test)/', '$1/'
                $segments = [Collections.Generic.List[string]]::new()
                foreach ($part in $source.Split('/')) {
                    if ($part -eq '.' -or $part -eq '') { continue }
                    if ($part -eq '..' -and $segments.Count -gt 0 -and $segments[-1] -ne '..') { $segments.RemoveAt($segments.Count - 1) }
                    else { $segments.Add($part) }
                }
                $source = $segments -join '/'
                $module = if ($source -match '(?i)^src/([^/]+)/') { $Matches[1] } else { $packageName }
                if ($owners.ContainsKey($source) -and $owners[$source] -ine $module) {
                    throw "Coverage source has conflicting module ownership: $source"
                }
                $owners[$source] = $module
                if (-not $modules.ContainsKey($module)) {
                    $modules[$module] = @{
                        Valid = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
                        Covered = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
                    }
                }
                # Method nodes repeat class lines; only canonical class lines are inputs.
                foreach ($line in $class.SelectNodes('lines/line')) {
                    $number = 0L; $hits = 0L
                    if (-not [long]::TryParse($line.GetAttribute('number'), [ref]$number) -or $number -lt 1 -or
                        -not [long]::TryParse($line.GetAttribute('hits'), [ref]$hits) -or $hits -lt 0) {
                        throw "Invalid coverage line or hit count: $source"
                    }
                    $key = "$source`:$number"
                    [void]$modules[$module].Valid.Add($key)
                    if ($hits -gt 0) { [void]$modules[$module].Covered.Add($key); $hasHit = $true }
                    $lineCount++
                }
            }
        }
        if (($valid -gt 0) -ne ($lineCount -gt 0) -or ($covered -gt 0) -ne $hasHit) {
            throw "Coverage counters have no matching source-line evidence: $($files[0].FullName)"
        }
    }
    foreach ($module in $modules.Keys | Sort-Object) {
        [pscustomobject]@{ Module = $module; Covered = $modules[$module].Covered.Count; Valid = $modules[$module].Valid.Count }
    }
}
