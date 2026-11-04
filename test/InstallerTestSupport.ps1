# Lifecycle/report contracts can be tested without installing anything.
. (Join-Path $PSScriptRoot 'UIOperationTestSupport.ps1')
function Invoke-InstallerLifecycle {
    param([scriptblock] $Install, [scriptblock] $Open, [scriptblock] $Uninstall,
        [scriptblock] $UIOperation = $null, $Environment = $null)
    $started = [DateTimeOffset]::UtcNow
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $checks = [Collections.Generic.List[object]]::new()
    $stages = @('Install', 'Open')
    if ($null -ne $UIOperation) { $stages += 'UIOperation' }
    $stages += 'Uninstall'
    foreach ($stage in $stages) {
        if ($stage -in @('Open', 'UIOperation') -and @($checks | Where-Object Status -EQ 'Failed').Count) {
            $checks.Add([pscustomobject]@{ Name = $stage; Passed = $false; Status = 'Skipped'; DurationMilliseconds = 0; Error = 'A prerequisite stage failed.' })
            continue
        }
        if ($stage -in @('Open', 'UIOperation') -and $null -ne $Environment -and $Environment.Available -ceq $false) {
            $checks.Add([pscustomobject]@{ Name = $stage; Passed = $false; Status = 'Skipped'; DurationMilliseconds = 0; Error = $Environment.Reason })
            continue
        }
        $stageTimer = [Diagnostics.Stopwatch]::StartNew()
        try {
            # Uninstall always runs after install, startup or UI failures.
            & (Get-Variable -Name $stage -ValueOnly) | Out-Host
            $checks.Add([pscustomobject]@{ Name = $stage; Passed = $true; Status = 'Passed'; DurationMilliseconds = $stageTimer.ElapsedMilliseconds; Error = $null })
        } catch {
            $checks.Add([pscustomobject]@{ Name = $stage; Passed = $false; Status = 'Failed'; DurationMilliseconds = $stageTimer.ElapsedMilliseconds; Error = $_.Exception.Message })
            Write-Warning "$stage failed: $($_.Exception.Message)"
        }
    }
    $passed = @($checks | Where-Object Passed).Count
    $skipped = @($checks | Where-Object Status -EQ 'Skipped').Count
    [pscustomobject]@{
        SchemaVersion = 1; Suite = 'InstallerLifecycle'; StartedAtUtc = $started
        DurationMilliseconds = $timer.ElapsedMilliseconds; Total = $stages.Count; Passed = $passed
        Failed = @($checks | Where-Object Status -EQ 'Failed').Count; Skipped = $skipped
        Checks = $checks.ToArray(); Environment = $Environment; UIOperationRequested = $null -ne $UIOperation
    }
}

function Get-InstallerReportOutcome {
    param($Report)
    $stages = @('Install', 'Open')
    if ($Report.UIOperationRequested -ceq $true) { $stages += 'UIOperation' }
    $stages += 'Uninstall'
    if ($null -eq $Report -or $Report.SchemaVersion -ne 1 -or $Report.Suite -ne 'InstallerLifecycle' -or
        $Report.Total -ne $stages.Count -or $Report.Failed -ne 0 -or @($Report.Checks).Count -ne $stages.Count -or
        (@($Report.Checks.Name) -join ',') -cne ($stages -join ',')) {
        throw 'Installer lifecycle acceptance is missing required stages or contains failures.'
    }
    if ($Report.Passed -eq $stages.Count -and @($Report.Checks | Where-Object { $_.Passed -cne $true -or $_.Status -cne 'Passed' }).Count -eq 0) {
        if ($null -ne $Report.Skipped -and $Report.Skipped -ne 0) { throw 'Inconsistent installer skip count.' }
        return 'Passed'
    }
    $environment = $Report.Environment
    if ($null -ne $environment -and $null -ne $environment.Checks) {
        $verified = Resolve-UIOperationEnvironment $environment.Checks
        if ($environment.Available -ceq $false -and $verified.Available -ceq $false -and
            $environment.ReasonCode -eq $verified.ReasonCode -and $environment.Reason -eq $verified.Reason -and
            $Report.Passed -eq 2 -and $Report.Skipped -eq ($stages.Count - 2) -and
            $Report.Checks[0].Passed -ceq $true -and $Report.Checks[0].Status -ceq 'Passed' -and
            @($Report.Checks | Where-Object { $_.Name -in @('Open', 'UIOperation') -and
                ($_.Passed -cne $false -or $_.Status -cne 'Skipped' -or $_.Error -ne $verified.Reason) }).Count -eq 0 -and
            $Report.Checks[-1].Passed -ceq $true -and $Report.Checks[-1].Status -ceq 'Passed') { return 'OpenSkipped' }
    }
    throw 'Installer lifecycle failed or has no valid desktop-unavailable evidence for skipping Open.'
}

function Assert-InstallerReportPassed {
    param($Report)
    $null = Get-InstallerReportOutcome $Report
}
