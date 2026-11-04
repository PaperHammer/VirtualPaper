# The lifecycle and report contract are shared by both workflows and can be tested without installing anything.
. (Join-Path $PSScriptRoot 'UIOperationTestSupport.ps1')
function Invoke-InstallerLifecycle {
    param([scriptblock] $Install, [scriptblock] $Open, [scriptblock] $Uninstall, $Environment = $null)
    $started = [DateTimeOffset]::UtcNow
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $checks = [Collections.Generic.List[object]]::new()
    foreach ($stage in @('Install', 'Open', 'Uninstall')) {
        if ($stage -eq 'Open' -and -not $checks[0].Passed) {
            $checks.Add([pscustomobject]@{ Name = $stage; Passed = $false; Status = 'Skipped'; DurationMilliseconds = 0; Error = 'Installation failed.' })
            continue
        }
        if ($stage -eq 'Open' -and $null -ne $Environment -and $Environment.Available -ceq $false) {
            $checks.Add([pscustomobject]@{ Name = $stage; Passed = $false; Status = 'Skipped'; DurationMilliseconds = 0; Error = $Environment.Reason })
            continue
        }
        $stageTimer = [Diagnostics.Stopwatch]::StartNew()
        try {
            # Uninstall is cleanup as well as acceptance, so it runs even after a failed install/open.
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
        DurationMilliseconds = $timer.ElapsedMilliseconds; Total = 3; Passed = $passed
        Failed = @($checks | Where-Object Status -EQ 'Failed').Count; Skipped = $skipped
        Checks = $checks.ToArray(); Environment = $Environment
    }
}

function Get-InstallerReportOutcome {
    param($Report)
    if ($null -eq $Report -or $Report.SchemaVersion -ne 1 -or $Report.Suite -ne 'InstallerLifecycle' -or
        $Report.Total -ne 3 -or $Report.Failed -ne 0 -or @($Report.Checks).Count -ne 3 -or
        (@($Report.Checks.Name) -join ',') -cne 'Install,Open,Uninstall') {
        throw 'Installer lifecycle acceptance requires successful Install, Open and Uninstall results.'
    }
    if ($Report.Passed -eq 3 -and @($Report.Checks | Where-Object { $_.Passed -cne $true -or $_.Status -cne 'Passed' }).Count -eq 0) {
        if ($null -ne $Report.Skipped -and $Report.Skipped -ne 0) { throw 'Inconsistent installer skip count.' }
        return 'Passed'
    }
    $environment = $Report.Environment
    if ($null -ne $environment -and $null -ne $environment.Checks) {
        $verified = Resolve-UIOperationEnvironment $environment.Checks
        if ($environment.Available -ceq $false -and $verified.Available -ceq $false -and
            $environment.ReasonCode -eq $verified.ReasonCode -and $environment.Reason -eq $verified.Reason -and
            $Report.Passed -eq 2 -and $Report.Skipped -eq 1 -and
            $Report.Checks[0].Passed -ceq $true -and $Report.Checks[0].Status -ceq 'Passed' -and
            $Report.Checks[1].Passed -ceq $false -and $Report.Checks[1].Status -ceq 'Skipped' -and
            $Report.Checks[1].Error -eq $verified.Reason -and
            $Report.Checks[2].Passed -ceq $true -and $Report.Checks[2].Status -ceq 'Passed') { return 'OpenSkipped' }
    }
    throw 'Installer lifecycle failed or has no valid desktop-unavailable evidence for skipping Open.'
}

function Assert-InstallerReportPassed {
    param($Report)
    $null = Get-InstallerReportOutcome $Report
}
