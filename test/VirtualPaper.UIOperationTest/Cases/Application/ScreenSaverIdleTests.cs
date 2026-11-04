using System.Diagnostics;
using System.Windows.Automation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class ScreenSaverIdleTests {
    public static async Task RunAsync(UiTestContext context) {
        string source = Path.Combine(context.Root, "idle-wallpaper.png");
        RenderingTests.WriteScene(source);
        await using var session = new AppProcessSession();
        await session.StartAsync(settings => {
            settings.WaitingTime = 1; settings.IsRunningLock = false;
            // Screen saver activity must not depend on forwarding movement to wallpapers.
            settings.MouseInputMovAlways = false;
        });
        context.Evidence["CoreProcessId"] = session.CoreId;
        var fixture = await WallpaperFixture.ImportAsync(session, source);
        await fixture.ApplyAsync();
        await Task.Run(() => {
            ApplicationUi.Select(ApplicationUi.Find(fixture.Root, "Library.Tab.ScreenSaver"));
            var toggle = (TogglePattern)ApplicationUi.Find(fixture.Root, "ScreenSaver.Enabled").GetCurrentPattern(TogglePattern.Pattern);
            Check(toggle.Current.ToggleState == ToggleState.Off, "Screen saver must begin disabled");
            toggle.Toggle();
        });
        await AppProcessSession.WaitForJsonAsync(session.SettingsPath, json => json["IsScreenSaverOn"]?.GetValue<bool>() == true);
        var rounds = new List<object>();
        context.Evidence["IdleRounds"] = rounds;
        for (int round = 0; round < 2; round++) {
            var waiting = Stopwatch.StartNew();
            Process? saver = null;
            nint window = 0;
            await AppProcessSession.WaitAsync(() => {
                var lastInput = new Native.LASTINPUTINFO { cbSize = (uint)Native.LASTINPUTINFO.SizeOf };
                if (Native.GetLastInputInfo(ref lastInput))
                    context.Evidence["ObservedOsIdleSeconds"] = unchecked((uint)Environment.TickCount - lastInput.dwTime) / 1000d;
                saver ??= session.FindChild(Constants.ModuleName.ScrSaver);
                if (saver == null) return Task.FromResult(false);
                Check(!saver.HasExited, "Screen saver exited before showing a window");
                window = DesktopPlayerWindow.FindVisibleTopLevel(saver.Id);
                return Task.FromResult(window != 0);
            }, "Real one-minute inactivity launches the owned screen saver", TimeSpan.FromMinutes(2));
            using var owned = saver!;
            _ = owned.Handle;
            Check(waiting.Elapsed >= TimeSpan.FromSeconds(50), "Screen saver launched before its real idle interval");
            await AppProcessSession.WaitAsync(async () => {
                string log = string.Join('\n', await Task.WhenAll(Directory.GetFiles(Constants.CommonPaths.LogDirCore, "*.txt")
                    .Select(AppProcessSession.ReadTextSharedAsync)));
                return log.Split('\n').Count(line => line.Contains("ScreenSaver:") && line.Contains("msg_wploaded") && line.Contains("true")) > round;
            }, "Real screen saver confirms its resource loaded");
            context.Evidence["ScreenSaverProcessId"] = owned.Id;
            WindowSnapshot.Save(window, Path.Combine(Constants.CommonPaths.LogDir, "UIOperation", $"idle-screen-saver-{round}.png"));
            await Task.Run(() => ApplicationUi.MovePointerOverOwnedWindow(window));
            await owned.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            Check(owned.ExitCode == 0, $"Screen saver exit code: {owned.ExitCode}");
            rounds.Add(new { ProcessId = owned.Id, IdleSeconds = waiting.Elapsed.TotalSeconds, ExitedNormally = true });
        }
        await Task.Run(() => {
            var toggle = (TogglePattern)ApplicationUi.Find(fixture.Root, "ScreenSaver.Enabled").GetCurrentPattern(TogglePattern.Pattern);
            if (toggle.Current.ToggleState == ToggleState.On) toggle.Toggle();
        });
        await AppProcessSession.WaitForJsonAsync(session.SettingsPath, json => json["IsScreenSaverOn"]?.GetValue<bool>() == false);
        await session.StopAsync();
        session.RecordEvidence(context);
        Check(((string[])context.Evidence["RuntimeErrors"]!).Length == 0, "Real idle flow logged runtime errors");
        context.Evidence["RealIdleLaunchAndInputWakeVerified"] = true;
    }
}
