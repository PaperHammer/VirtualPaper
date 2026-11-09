function Get-MsixAcceptanceStages {
    'Contracts', 'Build', 'ProjectTests', 'Package', 'Install', 'FirstRun', 'ExtendedUI',
    'Reinstall', 'RetainedProfile', 'FreshProfile', 'Uninstall', 'CertificateCleanup'
}

function Assert-MsixAcceptanceReport($Report) {
    if ($Report.Passed -ne $true -or $Report.PackageSha256 -notmatch '^[0-9a-fA-F]{64}$' -or
        -not $Report.PackageFullName -or $Report.Error -or
        @($Report.Stages).Count -ne @(Get-MsixAcceptanceStages).Count) {
        throw 'MSIX acceptance is incomplete or failed.'
    }
    foreach ($name in Get-MsixAcceptanceStages) {
        $stages = @($Report.Stages | Where-Object Name -CEQ $name)
        if ($stages.Count -ne 1 -or $stages[0].Status -cne 'Passed') {
            throw "MSIX acceptance requires $name to pass without skips."
        }
    }
}

function Assert-MsixUIReport([string] $Path, [string] $PackageFullName, [switch] $Extended) {
    $report = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ((Get-UIOperationReportOutcome $report) -ne 'Passed') { throw 'Required MSIX UI acceptance was skipped.' }
    $application = @($report.Results | Where-Object Name -Like 'Application.*')
    if (@($application | Where-Object {
        $_.Evidence.PackageFullName -cne $PackageFullName -or $_.Evidence.PackageActivation -cne 'AUMID' -or
        $_.Evidence.UnitTestBehaviorOverridesEnabled -ne $false
    }).Count) { throw 'MSIX UI evidence must prove installed package identity, shell activation and real behavior.' }
    if ($Extended) {
        foreach ($module in @('GeneralSettings', 'WallpaperLibrary', 'StaticEditor', 'WebEditor',
            'ModelInference', 'ScreenSaverLifecycle', 'Rendering')) {
            if (@($application | Where-Object Name -Like "Application.$module.*").Count -lt 1) {
                throw "Missing required installed MSIX UI module: $module"
            }
        }
    }
    return $report
}

function Assert-NoMsixTestProcesses([string] $InstallLocation) {
    $prefix = [IO.Path]::GetFullPath($InstallLocation).TrimEnd('\') + '\'
    foreach ($process in Get-Process -Name 'VirtualPaper*' -ErrorAction SilentlyContinue) {
        if ($process.HasExited) { continue }
        if ($process.Path -and $process.Path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Test package process is still running: $($process.Id). Reinstall/uninstall cannot count as a normal exit."
        }
    }
}

function Stop-OwnedMsixTestProcesses([string] $InstallLocation) {
    $prefix = [IO.Path]::GetFullPath($InstallLocation).TrimEnd('\') + '\'
    $forced = $false
    foreach ($process in Get-Process -Name 'VirtualPaper*' -ErrorAction SilentlyContinue) {
        if ($process.HasExited) { continue }
        if ($process.Path -and $process.Path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            $forced = $true
            $process.Kill($true)
            if (-not $process.WaitForExit(10000)) { throw "Owned package process did not stop: $($process.Id)" }
        }
    }
    return $forced
}
