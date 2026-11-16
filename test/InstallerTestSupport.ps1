# Lifecycle/report contracts can be tested without installing anything.
. (Join-Path $PSScriptRoot 'UIOperationTestSupport.ps1')
. (Join-Path $PSScriptRoot '../localization/LanguageCatalog.ps1')
function Get-InstallerApplicationLanguage([string]$Language) {
    $matches = @((Get-LanguageCatalog).Languages | Where-Object {
        $_.Code -ieq $Language -or $_.InstallerName -ieq $Language -or $_.Aliases -icontains $Language
    })
    if ($matches.Count -ne 1) { throw "Unsupported installer language: $Language" }
    return $matches[0].Code
}
function Get-InstallerLanguageSequence([string]$Language) {
    $first = $Language.ToLowerInvariant()
    if ($first -cnotin @('english', 'chinese', 'french')) { throw "Unsupported wizard acceptance language: $Language" }
    $second = if ($first -ceq 'english') { 'chinese' } else { 'english' }
    return @{ First = $first; Second = $second; FirstApplication = Get-InstallerApplicationLanguage $first; SecondApplication = Get-InstallerApplicationLanguage $second }
}
function Get-InstallerAppId([string]$SetupScriptPath) {
    $setupScript = Get-Content -LiteralPath $SetupScriptPath -Raw
    $appId = [regex]::Match($setupScript, '(?m)^#define AppId "\{\{([0-9A-Fa-f-]+)\}"').Groups[1].Value
    if (-not $appId) { throw 'Cannot determine the production installer AppId.' }
    return $appId
}

function Get-InstallRegistrations([string]$AppId) {
    $keyPath = "Software\Microsoft\Windows\CurrentVersion\Uninstall\{$AppId}_is1"
    foreach ($hive in @([Microsoft.Win32.RegistryHive]::LocalMachine, [Microsoft.Win32.RegistryHive]::CurrentUser)) {
        foreach ($view in @([Microsoft.Win32.RegistryView]::Registry64, [Microsoft.Win32.RegistryView]::Registry32)) {
            $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey($hive, $view)
            try {
                $key = $root.OpenSubKey($keyPath)
                if ($null -ne $key) {
                    try { [pscustomobject]@{ Location = $key.GetValue('InstallLocation'); Version = $key.GetValue('DisplayVersion') } }
                    finally { $key.Dispose() }
                }
            } finally { $root.Dispose() }
        }
    }
}

function Get-InstallerShortcutPaths {
    @([Environment]::GetFolderPath('CommonPrograms'), [Environment]::GetFolderPath('Programs')) |
        ForEach-Object { Join-Path $_ 'Virtual Paper.lnk' } | Sort-Object -Unique
}

function Assert-CleanInstallerEnvironment([string]$SetupScriptPath) {
    $appId = Get-InstallerAppId $SetupScriptPath
    $registrations = @(Get-InstallRegistrations $appId)
    if ($registrations.Count) {
        throw "An existing Virtual Paper installation was found: $($registrations.Location -join '; '). Uninstall it explicitly or use a clean test machine."
    }
    $shortcuts = @(Get-InstallerShortcutPaths | Where-Object { Test-Path -LiteralPath $_ })
    if ($shortcuts.Count) { throw "An existing Virtual Paper Start menu shortcut was found: $($shortcuts -join '; '). Use a clean test machine." }
}

function Invoke-InstallerLifecycle {
    param([scriptblock] $Install, [scriptblock] $Open, [scriptblock] $Uninstall,
        [Parameter(Mandatory)][scriptblock] $Reinstall,
        [Parameter(Mandatory)][scriptblock] $VerifyPreserved,
        [Parameter(Mandatory)][scriptblock] $OpenFresh,
        $Environment = $null)
    $started = [DateTimeOffset]::UtcNow
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $checks = [Collections.Generic.List[object]]::new()
    $stages = @('Install', 'Open', 'Reinstall', 'VerifyPreserved', 'OpenFresh')
    $stages += 'Uninstall'
    foreach ($stage in $stages) {
        if ($stage -ne 'Uninstall' -and @($checks | Where-Object Status -EQ 'Failed').Count) {
            $checks.Add([pscustomobject]@{ Name = $stage; Passed = $false; Status = 'Skipped'; DurationMilliseconds = 0; Error = 'A prerequisite stage failed.' })
            continue
        }
        if ($stage -in @('Open', 'VerifyPreserved', 'OpenFresh') -and $null -ne $Environment -and $Environment.Available -ceq $false) {
            $checks.Add([pscustomobject]@{ Name = $stage; Passed = $false; Status = 'Skipped'; DurationMilliseconds = 0; Error = $Environment.Reason })
            continue
        }
        $stageTimer = [Diagnostics.Stopwatch]::StartNew()
        try {
            # Uninstall always runs after install or startup failures.
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
        SchemaVersion = 3; Suite = 'InstallerSmoke'; StartedAtUtc = $started
        DurationMilliseconds = $timer.ElapsedMilliseconds; Total = $stages.Count; Passed = $passed
        Failed = @($checks | Where-Object Status -EQ 'Failed').Count; Skipped = $skipped
        Checks = $checks.ToArray(); Environment = $Environment
    }
}

function Get-InstallerReportOutcome {
    param($Report)
    $stages = @('Install', 'Open', 'Reinstall', 'VerifyPreserved', 'OpenFresh', 'Uninstall')
    if ($null -eq $Report -or $Report.SchemaVersion -ne 3 -or $Report.Suite -cne 'InstallerSmoke' -or
        $Report.Total -ne $stages.Count -or $Report.Passed -ne $stages.Count -or
        $Report.Failed -ne 0 -or $Report.Skipped -ne 0 -or @($Report.Checks).Count -ne $stages.Count -or
        (@($Report.Checks.Name) -join ',') -cne ($stages -join ',') -or
        @($Report.Checks | Where-Object { $_.Passed -cne $true -or $_.Status -cne 'Passed' }).Count -ne 0) {
        throw 'Installer smoke requires install, startup/normal exit, reinstall, profile checks and uninstall to pass without skips.'
    }
    return 'Passed'
}

function Assert-InstallerReportPassed {
    param($Report)
    $null = Get-InstallerReportOutcome $Report
}
