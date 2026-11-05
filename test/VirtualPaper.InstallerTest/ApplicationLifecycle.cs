using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Automation;
using Google.Protobuf.WellKnownTypes;
using GrpcDotNetNamedPipes;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Service.Commands;
using VirtualPaper.Grpc.Service.UserSettings;

namespace VirtualPaper.InstallerTest;

internal static class ApplicationLifecycle {
    public static ApplicationResult Run(string installDir, TimeSpan timeout, string dataRoot, string profileMode = "first-run") {
        var result = new ApplicationResult { ProfileMode = profileMode };
        var clock = Stopwatch.StartNew();
        Process? core = null;
        Process? ui = null;
        Grpc_CommandsService.Grpc_CommandsServiceClient? commands = null;
        string corePath = Path.Combine(installDir, "VirtualPaper.exe");
        string uiPath = Path.Combine(installDir, "Plugins", "UI", "VirtualPaper.UI.exe");
        PreservedProfile? profile = null;
        try {
            if (!File.Exists(corePath) || !File.Exists(uiPath)) throw new FileNotFoundException("Installed Core/UI executable is missing.");
            Environment.SetEnvironmentVariable(TestProcessEnvironment.SessionVariable, Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable(TestProcessEnvironment.DataRootVariable, Path.GetFullPath(dataRoot));
            if (profileMode == "preserve") {
                profile = PreservedProfile.Load(dataRoot);
                profile.VerifySettings();
                profile.VerifyFiles(dataRoot);
            } else if (File.Exists(Constants.CommonPaths.UserSettingsPath)) {
                throw new InvalidOperationException("First-run acceptance requires a new isolated profile.");
            }
            string marker = File.ReadAllText(Path.Combine(installDir, Constants.CoreField.InstallerLanguageFileName)).Trim();
            result.ExpectedLanguage = profile?.Language ?? (marker switch {
                "english" => "en-US",
                "chinese" => "zh-CN",
                _ => throw new InvalidDataException($"Unsupported installer language: {marker}")
            });
            commands = new(new NamedPipeChannel(".", Constants.CoreField.GrpcPipeServerName));
            var settings = new Grpc_UserSettingsService.Grpc_UserSettingsServiceClient(new NamedPipeChannel(".", Constants.CoreField.GrpcPipeServerName));
            core = Process.Start(new ProcessStartInfo(corePath) { WorkingDirectory = installDir, UseShellExecute = false })
                ?? throw new InvalidOperationException("The installed application did not start.");
            result.CoreProcessId = core.Id;
            Console.WriteLine($"Started installed application: {corePath} (PID {core.Id})");
            if (profileMode == "preserve") {
                bool ready = false;
                while (clock.Elapsed < timeout && !core.HasExited) {
                    try { settings.GetSettings(new Empty(), deadline: DateTime.UtcNow.AddSeconds(1)); ready = true; break; }
                    catch (global::Grpc.Core.RpcException) { Thread.Sleep(100); }
                }
                if (!ready) throw new TimeoutException("Core RPC was not ready after reinstall.");
                commands.ShowUIAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(10)).ResponseAsync.GetAwaiter().GetResult();
            }
            // A new user profile must open UI through Core's normal first-run path.
            // Do not issue ShowUI or start UI.exe to hide a broken first-run experience.
            TimeSpan? readyAt = null;
            while (clock.Elapsed < timeout) {
                if (core.HasExited) throw new InvalidOperationException($"Core exited during first launch: {core.ExitCode}");
                ui ??= FindInstalledUi(uiPath);
                if (ui is not null) {
                    ui.Refresh();
                    if (ui.HasExited) throw new InvalidOperationException($"UI exited during first launch: {ui.ExitCode}");
                    if (ui.MainWindowHandle != IntPtr.Zero && ui.Responding) {
                        readyAt ??= clock.Elapsed;
                        if (clock.Elapsed - readyAt.Value >= TimeSpan.FromSeconds(3)) break;
                    } else readyAt = null;
                }
                Thread.Sleep(200);
            }
            if (ui is null || readyAt is null || clock.Elapsed - readyAt.Value < TimeSpan.FromSeconds(3))
                throw new TimeoutException("First launch did not produce a stable, responding UI window.");
            result.UiProcessId = ui.Id;
            result.OpenedNormally = true;
            Console.WriteLine($"First-run UI ready: PID {ui.Id}, HWND {ui.MainWindowHandle}");
            var liveSettings = settings.GetSettings(new Empty(), deadline: DateTime.UtcNow.AddSeconds(5));
            result.ActualLanguage = liveSettings.Language;
            if (result.ActualLanguage != result.ExpectedLanguage)
                throw new InvalidOperationException($"First-run language: expected {result.ExpectedLanguage}, got {result.ActualLanguage}.");
            var window = AutomationElement.FromHandle(ui.MainWindowHandle);
            var navigation = window.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "App.Navigation.Settings"))
                ?? throw new InvalidOperationException("The installed UI has no Settings navigation item.");
            ((SelectionItemPattern)navigation.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            string expectedTab = result.ExpectedLanguage == "zh-CN" ? "通用" : "General";
            bool localized = false;
            while (clock.Elapsed < timeout) {
                var tab = window.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "Settings.Tab.General"));
                if (tab?.Current.Name == expectedTab) { localized = true; break; }
                Thread.Sleep(100);
            }
            if (!localized) throw new InvalidOperationException("The installed UI did not display the expected profile language.");
            if (profileMode == "prepare") profile = PreservedProfile.Prepare(settings);
            if (profileMode == "preserve") {
                profile!.VerifySettings(liveSettings);
                var library = window.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "App.Navigation.Library"))
                    ?? throw new InvalidOperationException("Library navigation is missing.");
                ((SelectionItemPattern)library.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
                bool found = false;
                while (clock.Elapsed < timeout) {
                    if (window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty,
                        "Library.Item." + profile.WallpaperUid)) != null) { found = true; break; }
                    Thread.Sleep(100);
                }
                if (!found) throw new InvalidOperationException("The preserved wallpaper is missing from the real library UI.");
            }
            // WM_CLOSE uses the same production path as the user's window close button.
            if (!ui.CloseMainWindow()) throw new InvalidOperationException("UI rejected the normal close request.");
            if (!ui.WaitForExit(15000)) throw new TimeoutException("UI did not exit after its window was closed.");
            if (ui.ExitCode != 0) throw new InvalidOperationException($"UI exited abnormally: {ui.ExitCode}");
            if (core.HasExited) throw new InvalidOperationException("Core exited before the normal quit request.");
            try {
                commands.ShutDownAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(5)).ResponseAsync.GetAwaiter().GetResult();
            } catch (global::Grpc.Core.RpcException) {
                // The server may finish shutting down before its acknowledgement reaches this client.
                // Exit and exit-code checks below are still mandatory.
            }
            if (!core.WaitForExit(15000)) throw new TimeoutException("Core did not exit after the normal quit request.");
            if (core.ExitCode != 0) throw new InvalidOperationException($"Core exited abnormally: {core.ExitCode}");
            result.ExitedNormally = true;
            using var saved = JsonDocument.Parse(File.ReadAllText(Constants.CommonPaths.UserSettingsPath));
            result.PersistedLanguage = saved.RootElement.GetProperty("Language").GetString();
            if (result.PersistedLanguage != result.ExpectedLanguage)
                throw new InvalidOperationException("The installer-selected language was not persisted.");
            result.LanguageVerified = true;
            if (profileMode == "prepare") profile!.SaveBaseline(dataRoot);
            if (profileMode == "preserve") { profile!.VerifySettings(); profile.VerifyFiles(dataRoot); }
            result.PersistenceVerified = profile != null;
        } catch (Exception exception) {
            result.Error = exception.ToString();
            Console.Error.WriteLine(exception);
        } finally {
            // Forced cleanup is recovery only. It can never turn an unsuccessful check green.
            foreach (var process in new[] { ui, core }) {
                if (process is null) continue;
                try {
                    if (!process.HasExited) {
                        result.ForcedCleanup = true;
                        process.Kill(entireProcessTree: true);
                        if (!process.WaitForExit(10000)) throw new TimeoutException($"Process {process.Id} did not stop during cleanup.");
                    }
                } catch (Exception exception) {
                    result.Error = string.Join(Environment.NewLine, result.Error, $"Cleanup failed: {exception}");
                } finally { process.Dispose(); }
            }
        }
        result.DurationMilliseconds = clock.ElapsedMilliseconds;
        return result;
    }

    private static Process? FindInstalledUi(string uiPath) {
        foreach (var process in Process.GetProcessesByName("VirtualPaper.UI")) {
            try {
                if (string.Equals(process.MainModule?.FileName, uiPath, StringComparison.OrdinalIgnoreCase))
                    return ProcessAttachment.RetainHandle(process);
            } catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            process.Dispose();
        }
        return null;
    }
}
