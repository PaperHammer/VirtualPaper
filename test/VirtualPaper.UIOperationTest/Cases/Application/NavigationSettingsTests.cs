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
        var saved = await AppProcessSession.WaitForJsonAsync(session.SettingsPath,
            json => json["NavigationAnimationsEnabled"]?.GetValue<bool>() == false);
        Check(saved["NavigationAnimationsEnabled"]!.GetValue<bool>() == false, "Core JSON did not persist the UI change");

        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        await Task.Run(() => ToggleNavigation(ui, expected: false, toggle: false));
        context.Evidence["UiProcessIds"] = session.UiIds.ToArray();
        Check(session.UiIds.Distinct().Count() == 2, "The UI was not recreated in a new process");
        context.Evidence["SavedValue"] = false;
        context.Evidence["ReopenedValue"] = false;
        await session.StopAsync();
        session.RecordEvidence(context);
    }

    private static void ToggleNavigation(Process ui, bool expected, bool toggle = true) {
        var root = AutomationElement.FromHandle(ui.MainWindowHandle);
        ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Settings"));
        var control = ApplicationUi.Find(root, "Settings.General.NavigationAnimations");
        ApplicationUi.BringIntoView(control);
        var pattern = (TogglePattern)control.GetCurrentPattern(TogglePattern.Pattern);
        Check(pattern.Current.ToggleState == (expected ? ToggleState.On : ToggleState.Off), "UI toggle did not display the expected saved value");
        if (toggle) pattern.Toggle();
    }
}
