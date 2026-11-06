using System.IO.Pipes;
using System.Windows.Automation;
using VirtualPaper.Common;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class ScreenSaverLifecycleTests {
    public static async Task RunAsync(UiTestContext context) {
        await using var session = new AppProcessSession();
        await session.StartAsync();
        context.Evidence["CoreProcessId"] = session.CoreId;
        for (int iteration = 0; iteration < 12; iteration++) {
            var ui = await session.OpenUiAsync();
            var root = await Task.Run(() => AutomationElement.FromHandle(ui.MainWindowHandle));
            for (int visit = 0; visit < 2; visit++) {
                await Task.Run(() => {
                    ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Library"));
                    ApplicationUi.Select(ApplicationUi.Find(root, "Library.Tab.ScreenSaver"));
                    ApplicationUi.Find(root, "ScreenSaver.Whitelist.Search");
                });
                var before = await ReadNotificationCountsAsync();
                using (var client = new NamedPipeClientStream(".", Constants.PipeControlField.TrayCmdPipeName, PipeDirection.Out, PipeOptions.Asynchronous)) {
                    await client.ConnectAsync(3000);
                    using var writer = new StreamWriter(client);
                    await writer.WriteLineAsync(Constants.PipeControlField.CmdUpdateScrSettings);
                    await writer.FlushAsync();
                }
                await AppProcessSession.WaitAsync(async () => (await ReadNotificationCountsAsync())
                    .Any(pair => pair.Value > before.GetValueOrDefault(pair.Key)),
                    $"Screen saver notification acknowledged after visit {iteration}:{visit}");
                await Task.Run(() => ApplicationUi.Select(ApplicationUi.Find(root, "Nav_LibraryContents")));
            }
            await session.CloseUiAsync();
            context.Evidence["CompletedUiGenerations"] = iteration + 1;
        }
        await session.StopAsync();
        session.RecordEvidence(context);
        Check(((string[])context.Evidence["RuntimeErrors"]!).Length == 0, "Screen saver lifecycle logged a runtime error");
        context.Evidence["PageNotificationAndRepeatedCloseVerified"] = true;
    }

    private static async Task<Dictionary<string, int>> ReadNotificationCountsAsync() {
        // Files roll each second and older files are pruned. Compare individual files,
        // since deleting an old file can decrease the total while a new command arrives.
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.GetFiles(Constants.CommonPaths.LogDirUI, "*.txt")) {
            try {
                string log = await AppProcessSession.ReadTextSharedAsync(path);
                counts[path] = log.Split('\n').Count(line => line.Contains("[PipeServer] Received command:", StringComparison.Ordinal));
            }
            catch (FileNotFoundException) { } // NLog can prune a file after enumeration.
        }
        return counts;
    }
}
