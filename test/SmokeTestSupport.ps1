# The lifecycle and report contract are shared by both workflows and can be tested without installing anything.
function Invoke-SmokeLifecycle {
    param([scriptblock] $Install, [scriptblock] $Launch, [scriptblock] $Uninstall)
    $started = [DateTimeOffset]::UtcNow
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $checks = [Collections.Generic.List[object]]::new()
    foreach ($stage in @('Install', 'Launch', 'Uninstall')) {
        if ($stage -eq 'Launch' -and -not $checks[0].Passed) {
            $checks.Add([pscustomobject]@{ Name = $stage; Passed = $false; Status = 'Skipped'; DurationMilliseconds = 0; Error = 'Installation failed.' })
            continue
        }
        $stageTimer = [Diagnostics.Stopwatch]::StartNew()
        try {
            # Uninstall is cleanup as well as acceptance, so it runs even after a failed install/launch.
            & (Get-Variable -Name $stage -ValueOnly) | Out-Host
            $checks.Add([pscustomobject]@{ Name = $stage; Passed = $true; Status = 'Passed'; DurationMilliseconds = $stageTimer.ElapsedMilliseconds; Error = $null })
        } catch {
            $checks.Add([pscustomobject]@{ Name = $stage; Passed = $false; Status = 'Failed'; DurationMilliseconds = $stageTimer.ElapsedMilliseconds; Error = $_.Exception.Message })
            Write-Warning "$stage failed: $($_.Exception.Message)"
        }
    }
    $passed = @($checks | Where-Object Passed).Count
    [pscustomobject]@{
        SchemaVersion = 1; Suite = 'InstallLaunchUninstall'; StartedAtUtc = $started
        DurationMilliseconds = $timer.ElapsedMilliseconds; Total = 3; Passed = $passed
        Failed = 3 - $passed; Checks = $checks.ToArray()
    }
}

function Assert-SmokeReportPassed {
    param($Report)
    if ($null -eq $Report -or $Report.SchemaVersion -ne 1 -or $Report.Suite -ne 'InstallLaunchUninstall' -or
        $Report.Total -ne 3 -or $Report.Passed -ne 3 -or $Report.Failed -ne 0 -or @($Report.Checks).Count -ne 3 -or
        (@($Report.Checks.Name) -join ',') -cne 'Install,Launch,Uninstall' -or
        @($Report.Checks | Where-Object { $_.Passed -cne $true -or $_.Status -cne 'Passed' }).Count -ne 0) {
        throw 'Smoke acceptance requires successful Install, Launch and Uninstall results.'
    }
}
