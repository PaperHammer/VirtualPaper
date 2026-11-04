#Requires -Version 7.2
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { Write-Host '[SKIPPED] Windows process-handle contracts.'; return }
$fixture = Join-Path (Split-Path -Parent $PSScriptRoot) "TestResults/installer-process-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $fixture | Out-Null
$fixtureSource = Join-Path $fixture 'ProcessAttachmentRegression.cs'
# Compile the production helper into the same assembly as this headless regression.
# The child waits on stdin, ensuring attachment happens before it exits.
@'
#nullable enable
using System;
using System.Diagnostics;

namespace VirtualPaper.InstallerTest;

public static class ProcessAttachmentRegression {
    public static void Run() {
        foreach (bool byName in new[] { false, true }) {
            foreach (int exitCode in new[] { 0, 7 }) {
                var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec")!) {
                    Arguments = $"/d /c \"set /p gate= >nul & exit /b {exitCode}\"",
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true
                };
                using var owner = Process.Start(start) ?? throw new InvalidOperationException("Fixture did not start.");
                Process? attached = null;
                try {
                    if (byName) {
                        foreach (var candidate in Process.GetProcessesByName(owner.ProcessName)) {
                            if (candidate.Id == owner.Id) attached = candidate;
                            else candidate.Dispose();
                        }
                    } else attached = Process.GetProcessById(owner.Id);
                    if (attached is null) throw new InvalidOperationException("Fixture process was not found.");
                    using var tracked = ProcessAttachment.RetainHandle(attached);
                    owner.StandardInput.WriteLine("continue");
                    owner.StandardInput.Close();
                    if (!tracked.WaitForExit(5000)) throw new TimeoutException("Fixture did not exit.");
                    if (tracked.ExitCode != exitCode) throw new InvalidOperationException($"Exit code lost: expected {exitCode}, actual {tracked.ExitCode}.");
                } finally {
                    attached?.Dispose();
                    if (!owner.HasExited) { owner.Kill(true); owner.WaitForExit(5000); }
                }
            }
        }
    }
}
'@ | Set-Content -LiteralPath $fixtureSource -Encoding utf8
$helperSource = Join-Path $PSScriptRoot 'VirtualPaper.InstallerTest/ProcessAttachment.cs'
$types = Add-Type -Path @($helperSource, $fixtureSource) -PassThru
$regression = $types | Where-Object FullName -EQ 'VirtualPaper.InstallerTest.ProcessAttachmentRegression'
$regression.GetMethod('Run').Invoke($null, $null) | Out-Null
Write-Host '[OK] PID/name attachment retains normal and nonzero exit codes (4 cases; no UI, installer or registry operations).'
