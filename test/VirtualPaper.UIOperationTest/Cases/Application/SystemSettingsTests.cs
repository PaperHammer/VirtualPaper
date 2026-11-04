using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Windows.Automation;
using VirtualPaper.Common;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class SystemSettingsTests {
    public static async Task ExportLogsAndCancelAsync(UiTestContext context) {
        await using var session = new AppProcessSession();
        await session.StartAsync();
        context.Evidence["CoreProcessId"] = session.CoreId;
        var ui = await session.OpenUiAsync();
        nint ownerWindow = ui.MainWindowHandle;
        string output = Path.Combine(context.Root, "diagnostics.zip");
        var root = await Task.Run(() => {
            var window = AutomationElement.FromHandle(ownerWindow);
            ApplicationUi.Select(ApplicationUi.Find(window, "App.Navigation.Settings"));
            ApplicationUi.Select(ApplicationUi.Find(window, "Settings.Tab.System"));
            ApplicationUi.Invoke(ApplicationUi.Find(window, "Settings.System.ExportLogs"));
            NativeFileDialog.Cancel(ui, ownerWindow);
            return window;
        });
        Check(Directory.GetFiles(context.Root, "*.zip").Length == 0, "Cancelling log export created an archive");
        await Task.Run(() => {
            ApplicationUi.Invoke(ApplicationUi.Find(root, "Settings.System.ExportLogs"));
            NativeFileDialog.SaveFile(ui, ownerWindow, output);
        });
        string[] entries = [];
        await AppProcessSession.WaitAsync(() => {
            try {
                if (!File.Exists(output) || new FileInfo(output).Length < 22) return Task.FromResult(false);
                using var archive = ZipFile.OpenRead(output);
                entries = archive.Entries.Select(entry => entry.FullName).ToArray();
                return Task.FromResult(entries.Length > 0);
            }
            catch (IOException) { return Task.FromResult(false); }
            catch (InvalidDataException) { return Task.FromResult(false); }
        }, "Exported diagnostic ZIP completed");
        using (var archive = ZipFile.OpenRead(output)) {
            string Relative(string path) => Path.GetRelativePath(Constants.CommonPaths.AppDataDir, path).Replace('\\', '/');
            var settingsEntry = archive.GetEntry(Relative(session.SettingsPath));
            Check(settingsEntry != null, "Exported archive has no session settings");
            using var settingsReader = new StreamReader(settingsEntry!.Open());
            Check(JsonNode.DeepEquals(JsonNode.Parse(await settingsReader.ReadToEndAsync()),
                JsonNode.Parse(await File.ReadAllTextAsync(session.SettingsPath))), "Exported settings differ from the session settings");
            Check(archive.GetEntry(Relative(Constants.CommonPaths.WallpaperLayoutPath)) != null, "Exported archive has no wallpaper layout");
            foreach (string logFolder in new[] { Constants.CommonPaths.LogDirCore, Constants.CommonPaths.LogDirUI }) {
                var logs = Directory.GetFiles(logFolder, "*.txt");
                Check(logs.Length > 0, "Session did not generate expected application logs");
                Check(logs.Any(path => archive.GetEntry(Relative(path)) is { Length: > 0 }), "Exported archive is missing application log contents");
            }
            Check(entries.All(entry => !Path.IsPathRooted(entry) && !entry.Split('/').Contains("..")), "Exported ZIP contains unsafe paths");
        }
        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        await Task.Run(() => {
            root = AutomationElement.FromHandle(ui.MainWindowHandle);
            ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Settings"));
            ApplicationUi.Select(ApplicationUi.Find(root, "Settings.Tab.System"));
            ApplicationUi.Find(root, "Settings.System.ExportLogs", visible: true);
        });
        await session.StopAsync();
        session.RecordEvidence(context);
        Check(((string[])context.Evidence["RuntimeErrors"]!).Length == 0,
            "System settings workflow logged a runtime error; inspect the report evidence");
        context.Evidence["ArchiveEntries"] = entries;
        context.Evidence["CancelAndExportVerified"] = true;
        context.Evidence["DebugWindowTested"] = false;
    }
}
