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
                string log = Directory.GetFiles(Constants.CommonPaths.LogDirUI, "*.txt")
                    .OrderByDescending(File.GetLastWriteTimeUtc).First();
                int before = CountNotifications(await AppProcessSession.ReadTextSharedAsync(log));
                using (var client = new NamedPipeClientStream(".", Constants.PipeControlField.TrayCmdPipeName, PipeDirection.Out, PipeOptions.Asynchronous)) {
                    await client.ConnectAsync(3000);
                    using var writer = new StreamWriter(client);
                    await writer.WriteLineAsync(Constants.PipeControlField.CmdUpdateScrSettings);
                    await writer.FlushAsync();
                }
                await AppProcessSession.WaitAsync(async () => CountNotifications(await AppProcessSession.ReadTextSharedAsync(log)) > before,
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

    private static int CountNotifications(string log) => log.Split('\n').Count(line => line.Contains("[PipeServer] Received command:", StringComparison.Ordinal));
}
