using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Windows.Automation;
using Google.Protobuf.WellKnownTypes;
using VirtualPaper.Common;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class NavigationSettingsTests {
    public static async Task SaveAndReopenAsync(UiTestContext context) {
        await using var session = new AppProcessSession();
        await session.StartAsync();
        context.Evidence["CoreProcessId"] = session.CoreId;
        context.Evidence["SessionId"] = TestProcessEnvironment.SessionId;
        var ui = await session.OpenUiAsync();

        // UI Automation clients run on a separate MTA thread; the target is the real UI process.
        await Task.Run(() => ToggleNavigation(ui, expected: true));
        await AppProcessSession.WaitAsync(async () => {
            var settings = await session.Settings.GetSettingsAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(1));
            return !settings.NavigationAnimationsEnabled;
        }, "UI change persisted in Core");
        var saved = JsonNode.Parse(await File.ReadAllTextAsync(session.SettingsPath))!;
        Check(saved["NavigationAnimationsEnabled"]!.GetValue<bool>() == false, "Core JSON did not persist the UI change");

        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        await Task.Run(() => ToggleNavigation(ui, expected: false, toggle: false));
        context.Evidence["UiProcessIds"] = session.UiIds.ToArray();
        Check(session.UiIds.Distinct().Count() == 2, "The UI was not recreated in a new process");
        context.Evidence["SavedValue"] = false;
        context.Evidence["ReopenedValue"] = false;
        await session.StopAsync();
        context.Evidence["ProcessesExitedNormally"] = true;
        context.Evidence["RuntimeErrors"] = Directory.EnumerateFiles(Constants.CommonPaths.LogDir, "*.txt", SearchOption.AllDirectories)
            .SelectMany(File.ReadLines).Where(line => line.Contains("|ERROR|", StringComparison.Ordinal)).Distinct().ToArray();
    }

    private static void ToggleNavigation(Process ui, bool expected, bool toggle = true) {
        var root = AutomationElement.FromHandle(ui.MainWindowHandle);
        var settings = Find(root, "App.Navigation.Settings");
        if (settings.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selection))
            ((SelectionItemPattern)selection).Select();
        else
            ((InvokePattern)settings.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        var control = Find(root, "Settings.General.NavigationAnimations");
        if (control.TryGetCurrentPattern(ScrollItemPattern.Pattern, out var scroll))
            ((ScrollItemPattern)scroll).ScrollIntoView();
        var pattern = (TogglePattern)control.GetCurrentPattern(TogglePattern.Pattern);
        Check(pattern.Current.ToggleState == (expected ? ToggleState.On : ToggleState.Off), "UI toggle did not display the expected saved value");
        if (toggle) pattern.Toggle();
    }

    private static AutomationElement Find(AutomationElement root, string id) {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(10)) {
            var element = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, id));
            if (element != null && element.Current.IsEnabled) return element;
            Thread.Sleep(100);
        }
        throw new TimeoutException($"UI Automation element not found: {id}");
    }
}
