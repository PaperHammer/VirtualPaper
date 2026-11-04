#Requires -Version 7.2
[CmdletBinding()]
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/UIOperationTestSupport.ps1"
$environment = Get-UIOperationEnvironment
if (-not $environment.Available) {
    Write-Host "[SKIPPED] Wizard interaction contracts: $($environment.Reason)"
    return
}
# A disposable WPF wizard exercises UIA and loader -> child handoff. It never installs,
# uninstalls, writes registry, locks the screen or touches the real application.
$root = Join-Path (Split-Path -Parent $PSScriptRoot) "TestResults/installer-wizard-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $root | Out-Null
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>WinExe</OutputType><TargetFramework>net8.0-windows</TargetFramework><UseWPF>true</UseWPF><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $root 'Fixture.csproj')
@'
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

internal static class Program {
    [STAThread]
    public static int Main(string[] args) {
        if (!args.Contains("--child")) {
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
            foreach (string arg in args) start.ArgumentList.Add(arg);
            start.ArgumentList.Add("--child");
            using var child = Process.Start(start)!;
            child.WaitForExit();
            return child.ExitCode;
        }
        var app = new Application();
        var window = new Window { Title = "Virtual Paper wizard contract", Width = 540, Height = 260, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        bool uninstall = Environment.ProcessPath!.Contains("unins", StringComparison.OrdinalIgnoreCase);
        bool stuck = Environment.ProcessPath!.Contains("stuck", StringComparison.OrdinalIgnoreCase);
        int page = 0;
        void Render() {
            var panel = new StackPanel { Margin = new Thickness(25) };
            window.Content = panel;
            void Button(string title, Action click, bool enabled = true) {
                var button = new Button { Content = title, IsEnabled = enabled, Margin = new Thickness(5) };
                button.Click += (_, _) => click(); panel.Children.Add(button);
            }
            void Next() { page++; Render(); }
            if (stuck) { panel.Children.Add(new TextBlock { Text = "Unrecognized page; never continue automatically." }); return; }
            if (!uninstall) {
                if (page == 0) {
                    var accept = new RadioButton { Content = "I accept the agreement" };
                    var next = new Button { Content = "Next >", IsEnabled = false };
                    accept.Checked += (_, _) => next.IsEnabled = true;
                    next.Click += (_, _) => Next();
                    panel.Children.Add(accept); panel.Children.Add(next);
                } else if (page == 1) { Button("Next >", Next); }
                else if (page == 2) { Button("Install", Next); }
                else { Button("Finish", () => window.Close()); }
            } else {
                if (page == 0) {
                    panel.Children.Add(new TextBlock { Text = "Are you sure you want to completely remove Virtual Paper and all of its components?" });
                    Button("Yes", Next); Button("No", () => Environment.Exit(2));
                } else if (page == 1) {
                    panel.Children.Add(new TextBlock { Text = "Do you want to delete data folder?" });
                    Button("Yes", () => Environment.Exit(3)); Button("No", Next);
                } else {
                    panel.Children.Add(new TextBlock { Text = "Virtual Paper was successfully removed from your computer." });
                    Button("OK", () => window.Close());
                }
            }
        }
        Render();
        return app.Run(window);
    }
}
'@ | Set-Content -LiteralPath (Join-Path $root 'Program.cs')
$probeProject = Join-Path $PSScriptRoot 'VirtualPaper.InstallerTest/VirtualPaper.InstallerTest.csproj'
dotnet build $probeProject -c $Configuration --no-restore --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Wizard controller build failed.' }
dotnet build (Join-Path $root 'Fixture.csproj') -c Release --ignore-failed-sources -p:NuGetAudit=false --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Wizard fixture build failed.' }
$probe = Join-Path $PSScriptRoot "VirtualPaper.InstallerTest/bin/$Configuration/net8.0-windows10.0.19041.0/VirtualPaper.InstallerTest.dll"
$fixtureRoot = Join-Path $root 'bin/Release/net8.0-windows'
foreach ($case in @('install', 'uninstall', 'timeout')) {
    $name = switch ($case) { install { 'setupFixture.exe' } uninstall { 'uninsFixture.exe' } timeout { 'stuckFixture.exe' } }
    $executable = Join-Path $fixtureRoot $name
    Copy-Item -LiteralPath (Join-Path $fixtureRoot 'Fixture.exe') -Destination $executable
    $action = if ($case -eq 'uninstall') { '--wizard-uninstall' } else { '--wizard-install' }
    $timeout = if ($case -eq 'timeout') { 3 } else { 20 }
    $report = Join-Path $root "$case.json"
    dotnet $probe $action $executable --install-dir (Join-Path $root 'unused install path') --timeout-seconds $timeout --results-file $report
    $code = $LASTEXITCODE
    $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if ($case -eq 'timeout') {
        if ($code -eq 0 -or $result.Passed -ne $false -or $result.Error -notlike '*deadline*') { throw 'Unknown wizard page must time out and fail.' }
    } elseif ($code -ne 0 -or $result.Passed -ne $true) { throw "Wizard interaction fixture failed: $case. $($result.Error)" }
}
if (@(Get-Process -Name '*Fixture' -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) }).Count) {
    throw 'Wizard fixture process remains after normal exit or timeout recovery.'
}
Write-Host "[OK] Real wizard controls, child-process handoff, keep-data choice and timeout recovery. No installer/system changes. Results: $root"
