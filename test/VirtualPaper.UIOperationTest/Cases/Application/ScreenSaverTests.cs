using System.Diagnostics;
using System.Windows.Automation;
using Empty = Google.Protobuf.WellKnownTypes.Empty;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class ScreenSaverTests {
    public static async Task SettingsAndWhitelistAsync(UiTestContext context) {
        await using var session = new AppProcessSession();
        // Keep automatic idle launch outside this settings test; lock-on-resume
        // stays disabled in the isolated configuration throughout the case.
        await session.StartAsync(settings => settings.WaitingTime = 60);
        Check(Constants.PipeControlField.TrayCmdPipeName.EndsWith(TestProcessEnvironment.SessionId!, StringComparison.Ordinal), "Screen saver notification pipe is not isolated");
        context.Evidence["CoreProcessId"] = session.CoreId;
        var ui = await session.OpenUiAsync();
        nint ownerWindow = ui.MainWindowHandle;
        var root = await Task.Run(() => OpenScreenSaver(ui));
        await Task.Run(() => Toggle(root, expected: false));
        await WaitForSettingsAsync(session, settings => settings.IsScreenSaverOn);
        await Task.Run(() => {
            var options = ApplicationUi.Find(root, "ScreenSaver.Options");
            ((ExpandCollapsePattern)options.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();
            var number = ApplicationUi.Find(root, "ScreenSaver.WaitMinutes");
            ApplicationUi.BringIntoView(number);
            ((RangeValuePattern)number.GetCurrentPattern(RangeValuePattern.Pattern)).SetValue(30);
        });
        await WaitForSettingsAsync(session, settings => settings.WaitingTime == 30);
        string effectLabel = await Task.Run(() => ApplicationUi.SelectComboIndex(ApplicationUi.Find(root, "ScreenSaver.Effect"), 1, 2));
        await WaitForSettingsAsync(session, settings => settings.WaitingTime == 30 && (int)settings.ScreenSaverEffect == 1);

        string executable = Path.Combine(Environment.SystemDirectory, "notepad.exe");
        Check(File.Exists(executable), "Windows notepad fixture is unavailable");
        await Task.Run(() => {
            ApplicationUi.Invoke(ApplicationUi.Find(root, "ScreenSaver.Whitelist.Add"));
            NativeFileDialog.OpenFile(ui, ownerWindow, executable);
        });
        await WaitForSettingsAsync(session, settings => settings.WhiteListScr.Count == 1 && settings.WhiteListScr[0].ProcName == "notepad" &&
            string.Equals(settings.WhiteListScr[0].ProcPath, executable, StringComparison.OrdinalIgnoreCase));
        await Task.Run(() => {
            ApplicationUi.Find(root, "ScreenSaver.Whitelist.Remove", visible: true);
            ApplicationUi.SetText(ApplicationUi.Find(root, "ScreenSaver.Whitelist.Search"), "no-such-app");
            var empty = ApplicationUi.Find(root, "ScreenSaver.Whitelist.EmptyTitle", visible: true);
            Check(empty.Current.Name == LanguageUtil.GetI18n("ScreenSaver_WhiteListNoResults"), "Search did not display the no-results state");
            ApplicationUi.SetText(ApplicationUi.Find(root, "ScreenSaver.Whitelist.Search"), "note");
            ApplicationUi.Find(root, "ScreenSaver.Whitelist.Remove", visible: true);
            ApplicationUi.SetText(ApplicationUi.Find(root, "ScreenSaver.Whitelist.Search"), "");
        });
        var saved = await AppProcessSession.WaitForJsonAsync(session.SettingsPath, json =>
            json["IsScreenSaverOn"]?.GetValue<bool>() == true && json["WaitingTime"]?.GetValue<int>() == 30 &&
            json["WhiteListScr"]?.AsArray().Count == 1);
        string icon = saved["WhiteListScr"]![0]!["IconPath"]!.GetValue<string>();
        Check(string.Equals(saved["WhiteListScr"]![0]!["ProcPath"]!.GetValue<string>(), executable, StringComparison.OrdinalIgnoreCase), "Whitelist executable path was not persisted");
        Check(File.Exists(icon), "Whitelist icon was not written");
        Check(saved["IsRunningLock"]?.GetValue<bool>() == false, "Settings test unexpectedly enabled desktop locking");
        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        root = await Task.Run(() => OpenScreenSaver(ui));
        await Task.Run(() => {
            CheckToggle(root, expected: true);
            ((ExpandCollapsePattern)ApplicationUi.Find(root, "ScreenSaver.Options").GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();
            var number = ApplicationUi.Find(root, "ScreenSaver.WaitMinutes");
            Check(((RangeValuePattern)number.GetCurrentPattern(RangeValuePattern.Pattern)).Current.Value == 30, "Wait time did not survive UI reopening");
            Check(ApplicationUi.SelectedLabel(ApplicationUi.Find(root, "ScreenSaver.Effect")) == effectLabel, "Effect did not survive UI reopening");
            Check(string.Equals(ApplicationUi.Find(root, "ScreenSaver.Whitelist.Path", visible: true).Current.Name, executable, StringComparison.OrdinalIgnoreCase), "Whitelist executable path did not survive UI reopening");
            ApplicationUi.Invoke(ApplicationUi.Find(root, "ScreenSaver.Whitelist.Remove", visible: true));
            ApplicationUi.Find(root, "ScreenSaver.Whitelist.EmptyTitle", visible: true);
            Toggle(root, expected: true);
        });
        await WaitForSettingsAsync(session, settings => !settings.IsScreenSaverOn && settings.WhiteListScr.Count == 0);
        await AppProcessSession.WaitForJsonAsync(session.SettingsPath, json =>
            json["IsScreenSaverOn"]?.GetValue<bool>() == false && json["WhiteListScr"]?.AsArray().Count == 0);
        Check(!File.Exists(icon), "Removed whitelist icon is still on disk");
        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        await Task.Run(() => {
            var reopened = OpenScreenSaver(ui);
            CheckToggle(reopened, expected: false);
            ApplicationUi.Find(reopened, "ScreenSaver.Whitelist.EmptyTitle", visible: true);
        });
        await session.StopAsync();
        session.RecordEvidence(context);
        context.Evidence["WaitMinutes"] = 30;
        context.Evidence["WhitelistAddSearchRemoveAndReopenVerified"] = true;
        context.Evidence["AutomaticIdleLaunchTested"] = false;
    }

    private static AutomationElement OpenScreenSaver(Process ui) {
        var root = AutomationElement.FromHandle(ui.MainWindowHandle);
        ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Library"));
        ApplicationUi.Select(ApplicationUi.Find(root, "Library.Tab.ScreenSaver"));
        ApplicationUi.Find(root, "ScreenSaver.Enabled");
        return root;
    }

    private static void CheckToggle(AutomationElement root, bool expected) {
        var toggle = (TogglePattern)ApplicationUi.Find(root, "ScreenSaver.Enabled").GetCurrentPattern(TogglePattern.Pattern);
        Check(toggle.Current.ToggleState == (expected ? ToggleState.On : ToggleState.Off), "Screen saver enabled state differs from saved settings");
    }

    private static void Toggle(AutomationElement root, bool expected) {
        CheckToggle(root, expected);
        ((TogglePattern)ApplicationUi.Find(root, "ScreenSaver.Enabled").GetCurrentPattern(TogglePattern.Pattern)).Toggle();
    }

    private static Task WaitForSettingsAsync(AppProcessSession session, Func<Grpc_SettingsData, bool> predicate) =>
        AppProcessSession.WaitAsync(async () => predicate(await session.Settings.GetSettingsAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(1))), "Screen saver settings in Core");
}
