function Get-MsixAcceptanceStages([ValidateSet('Full', 'Smoke', 'UI')][string] $Mode = 'Full') {
    if ($Mode -eq 'Full') { 'Contracts', 'Build', 'ProjectTests', 'Package' }
    'Install'
    if ($Mode -ne 'UI') { 'FirstRun' }
    if ($Mode -ne 'Smoke') { 'ExtendedUI' }
    if ($Mode -ne 'UI') { 'Reinstall', 'RetainedProfile', 'FreshProfile' }
    'Uninstall', 'CertificateCleanup'
}

function Assert-MsixAcceptanceReport($Report, [ValidateSet('Full', 'Smoke', 'UI')][string] $Mode = 'Full') {
    $actualMode = if ($Report.Mode) { $Report.Mode } else { 'Full' }
    if ($Report.Passed -ne $true -or $Report.PackageSha256 -notmatch '^[0-9a-fA-F]{64}$' -or
        -not $Report.PackageFullName -or $Report.Error -or $actualMode -cne $Mode -or
        @($Report.Stages).Count -ne @(Get-MsixAcceptanceStages $Mode).Count) {
        throw 'MSIX acceptance is incomplete or failed.'
    }
    foreach ($name in Get-MsixAcceptanceStages $Mode) {
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
    if ($Extended) { Assert-ExtendedUIModules $report }
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

function Initialize-MsixTestFramework([string] $ResultsRoot) {
    $name = 'Microsoft.VCLibs.140.00.UWPDesktop'
    $minimum = [version]'14.0.30704.0'
    $publisher = 'CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US'
    $installed = @(Get-AppxPackage -Name $name | Where-Object { [version]$_.Version -ge $minimum -and $_.Publisher -ceq $publisher -and $_.Architecture -ieq 'x64' })
    $origin = 'Preinstalled'
    if (-not $installed.Count) {
        if ($env:GITHUB_ACTIONS -cne 'true' -or $env:RUNNER_ENVIRONMENT -cne 'github-hosted') {
            throw 'Install Microsoft.VCLibs.140.00.UWPDesktop >= 14.0.30704.0 before local MSIX acceptance.'
        }
        # A clean CI VM may have VC++ desktop runtimes without the AppX framework.
        # Only disposable CI installs this shared prerequisite; local runs do not.
        $path = Join-Path $ResultsRoot 'Microsoft.VCLibs.x64.14.00.Desktop.appx'
        Invoke-WebRequest -Uri 'https://aka.ms/Microsoft.VCLibs.x64.14.00.Desktop.appx' -OutFile $path -TimeoutSec 120
        $archive = [IO.Compression.ZipFile]::OpenRead($path)
        try {
            $reader = [IO.StreamReader]::new($archive.GetEntry('AppxManifest.xml').Open())
            try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
            if ($manifest.Package.Identity.Name -cne $name -or $manifest.Package.Identity.Publisher -cne $publisher -or
                [version]$manifest.Package.Identity.Version -lt $minimum -or $manifest.Package.Identity.ProcessorArchitecture -cne 'x64') {
                throw 'The downloaded AppX is not the expected Microsoft VC framework.'
            }
        } finally { $archive.Dispose() }
        Add-AppxPackage -Path $path -ErrorAction Stop # Windows verifies Microsoft's signature.
        $origin = 'InstalledOnDisposableCI'
        $installed = @(Get-AppxPackage -Name $name | Where-Object { [version]$_.Version -ge $minimum -and $_.Publisher -ceq $publisher -and $_.Architecture -ieq 'x64' })
        if (-not $installed.Count) { throw 'The required Microsoft VC AppX framework is still unavailable.' }
    }
    return @{ Name = $name; Version = ($installed | Sort-Object Version -Descending | Select-Object -First 1).Version.ToString(); Origin = $origin }
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
