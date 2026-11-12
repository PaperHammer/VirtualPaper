using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Windows.Automation;
using Empty = Google.Protobuf.WellKnownTypes.Empty;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class PerformanceSettingsTests {
    private sealed record Rule(string Id, string JsonProperty, int Count, Func<Grpc_SettingsData, int> Read, System.Type? JsonEnum = null);
    private static readonly Rule[] Rules = [
        new("Fullscreen", "AppFullscreen", 3, settings => (int)settings.AppFullscreenPause),
        new("Focus", "AppFocus", 3, settings => (int)settings.AppFocusPause),
        new("Battery", "BatteryPoweredn", 3, settings => (int)settings.BatteryPause),
        new("PowerSaving", "PowerSaving", 3, settings => (int)settings.PowerSaveModePause),
        new("RemoteDesktop", "RemoteDesktop", 3, settings => (int)settings.RemoteDesktopPause),
        new("MonitorPolicy", "StatuMechanism", 2, settings => (int)settings.StatuMechanism, typeof(StatuMechanismEnum))
    ];

    public static async Task SaveAndReopenAsync(UiTestContext context) {
        await using var session = new AppProcessSession();
        await session.StartAsync();
        context.Evidence["CoreProcessId"] = session.CoreId;
        var initial = await session.Settings.GetSettingsAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(1));
        var expected = Rules.ToDictionary(rule => rule.Id, rule => (rule.Read(initial) + 1) % rule.Count);
        var labels = new Dictionary<string, string>();
        var ui = await session.OpenUiAsync();
        var root = await Task.Run(() => OpenPerformance(ui));
        Check(initial.HasUiHardwareAccelerationEnabled && initial.UiHardwareAccelerationEnabled, "Hardware acceleration did not start enabled");
        await Task.Run(() => {
            Check(!RestartNoticeVisible(root), "Unchanged hardware setting requires a restart");
            var toggle = (TogglePattern)ApplicationUi.Find(root, "Settings.Performance.HardwareAcceleration").GetCurrentPattern(TogglePattern.Pattern);
            Check(toggle.Current.ToggleState == ToggleState.On, "Hardware control did not load the enabled preference");
            toggle.Toggle();
        });
        await WaitHardwarePreferenceAsync(session, false);
        await AppProcessSession.WaitAsync(() => Task.Run(() => RestartNoticeVisible(root)), "Hardware setting restart notice");
        await Task.Run(() => ((TogglePattern)ApplicationUi.Find(root, "Settings.Performance.HardwareAcceleration").GetCurrentPattern(TogglePattern.Pattern)).Toggle());
        await WaitHardwarePreferenceAsync(session, true);
        await AppProcessSession.WaitAsync(() => Task.Run(() => !RestartNoticeVisible(root)), "Reverting hardware setting clears restart notice");
        await Task.Run(() => ((TogglePattern)ApplicationUi.Find(root, "Settings.Performance.HardwareAcceleration").GetCurrentPattern(TogglePattern.Pattern)).Toggle());
        await WaitHardwarePreferenceAsync(session, false);
        await AppProcessSession.WaitAsync(() => Task.Run(() => RestartNoticeVisible(root)), "Restored pending hardware setting");
        ui.Refresh();
        Check(!ui.HasExited && session.UiIds.Distinct().Count() == 1, "Saving hardware preference restarted the UI");
        foreach (var rule in Rules) {
            labels[rule.Id] = await Task.Run(() => ApplicationUi.SelectComboIndex(
                ApplicationUi.Find(root, "Settings.Performance." + rule.Id), expected[rule.Id], rule.Count));
            await AppProcessSession.WaitAsync(async () => rule.Read(await session.Settings.GetSettingsAsync(
                new Empty(), deadline: DateTime.UtcNow.AddSeconds(1))) == expected[rule.Id], $"Persist {rule.Id} in Core");
        }
        await Task.Run(() => {
            var audio = ApplicationUi.Find(root, "Settings.Performance.AudioOnlyOnDesktop");
            ApplicationUi.BringIntoView(audio);
            var toggle = (TogglePattern)audio.GetCurrentPattern(TogglePattern.Pattern);
            Check(toggle.Current.ToggleState == ToggleState.Off, "Audio policy did not start disabled");
            toggle.Toggle();
        });
        await AppProcessSession.WaitAsync(async () => (await session.Settings.GetSettingsAsync(
            new Empty(), deadline: DateTime.UtcNow.AddSeconds(1))).IsAudioOnlyOnDesktop, "Persist audio policy in Core");

        var saved = await AppProcessSession.WaitForJsonAsync(session.SettingsPath, json =>
            json["UiHardwareAccelerationEnabled"]?.GetValue<bool>() == false
            && json["IsAudioOnlyOnDesktop"]?.GetValue<bool>() == true && Rules.All(rule =>
                json[rule.JsonProperty]?.GetValue<string>() == Enum.GetName(rule.JsonEnum ?? typeof(AppWpRunRulesEnum), expected[rule.Id])));
        foreach (var rule in Rules)
            Check(saved[rule.JsonProperty]!.GetValue<string>() == Enum.GetName(rule.JsonEnum ?? typeof(AppWpRunRulesEnum), expected[rule.Id]),
                $"Core JSON did not save {rule.Id}");
        Check(saved["IsAudioOnlyOnDesktop"]!.GetValue<bool>(), "Core JSON did not save audio policy");
        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        await Task.Run(() => {
            var reopened = OpenPerformance(ui);
            Check(((TogglePattern)ApplicationUi.Find(reopened, "Settings.Performance.HardwareAcceleration").GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState == ToggleState.Off,
                "Reopened UI did not restore hardware acceleration preference");
            Check(!RestartNoticeVisible(reopened), "Reopened UI did not capture the new startup policy");
            foreach (var rule in Rules) {
                var combo = ApplicationUi.Find(reopened, "Settings.Performance." + rule.Id);
                Check(ApplicationUi.SelectedLabel(combo) == labels[rule.Id], $"Reopened UI did not restore {rule.Id}");
            }
            var audio = ApplicationUi.Find(reopened, "Settings.Performance.AudioOnlyOnDesktop");
            Check(((TogglePattern)audio.GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState == ToggleState.On,
                "Reopened UI did not restore audio policy");
        });
        Check(session.UiIds.Distinct().Count() == 2, "UI process was not recreated");
        await session.StopAsync();
        session.RecordEvidence(context);
        context.Evidence["SavedRules"] = Rules.ToDictionary(rule => rule.JsonProperty,
            rule => Enum.GetName(rule.JsonEnum ?? typeof(AppWpRunRulesEnum), expected[rule.Id]));
        context.Evidence["AudioOnlyOnDesktop"] = true;
        context.Evidence["ReopenedControlsVerified"] = 8;
        context.Evidence["HardwarePreferencePendingRestartRevertAndReopenVerified"] = true;
    }

    private static Task WaitHardwarePreferenceAsync(AppProcessSession session, bool enabled) => AppProcessSession.WaitAsync(async () =>
        (await session.Settings.GetSettingsAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(1))).UiHardwareAccelerationEnabled == enabled,
        "Persist hardware acceleration " + enabled);

    private static bool RestartNoticeVisible(AutomationElement root) => root.FindAll(TreeScope.Descendants,
        new PropertyCondition(AutomationElement.AutomationIdProperty, "Settings.Performance.HardwareAccelerationRestart"))
        .Cast<AutomationElement>().Any(element => !element.Current.IsOffscreen && element.Current.BoundingRectangle.Height > 0);

    private static AutomationElement OpenPerformance(Process ui) {
        var root = AutomationElement.FromHandle(ui.MainWindowHandle);
        ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Settings"));
        ApplicationUi.Select(ApplicationUi.Find(root, "Settings.Tab.Performance"));
        return root;
    }
}
