using System.Windows.Automation;
using Empty = Google.Protobuf.WellKnownTypes.Empty;
using VirtualPaper.Common;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class GeneralSettingsTests {
    public static async Task SaveLanguageThemeAndReopenAsync(UiTestContext context) {
        await using var session = new AppProcessSession();
        await session.StartAsync(settings => {
            settings.ApplicationTheme = AppTheme.Light;
            settings.SystemBackdrop = AppSystemBackdrop.Default;
        });
        context.Evidence["CoreProcessId"] = session.CoreId;
        var ui = await session.OpenUiAsync();
        var root = await Task.Run(() => {
            var window = AutomationElement.FromHandle(ui.MainWindowHandle);
            ApplicationUi.Select(ApplicationUi.Find(window, "App.Navigation.Settings"));
            ApplicationUi.Invoke(ApplicationUi.Find(window, "App.Theme"));
            return window;
        });
        await AppProcessSession.WaitAsync(async () => (int)(await session.Settings.GetSettingsAsync(new Empty(),
            deadline: DateTime.UtcNow.AddSeconds(1))).ApplicationTheme == (int)AppTheme.Dark, "Theme saved through Core");
        await Task.Run(() => ApplicationUi.SelectComboIndex(ApplicationUi.Find(root, "Settings.General.Backdrop"),
            (int)AppSystemBackdrop.Acrylic, 3, closesWindow: true));
        await AppProcessSession.WaitAsync(async () => (int)(await session.Settings.GetSettingsAsync(new Empty(),
            deadline: DateTime.UtcNow.AddSeconds(1))).SystemBackdrop == (int)AppSystemBackdrop.Acrylic, "Backdrop saved through Core");
        ui = await session.WaitForUiRestartAsync();
        root = await Task.Run(() => OpenGeneral(ui.MainWindowHandle));
        await Task.Run(() => {
            var autoStart = Toggle(root);
            Check(autoStart.Current.ToggleState == ToggleState.Off, "Auto start did not begin disabled");
            autoStart.Toggle();
        });
        await AppProcessSession.WaitAsync(async () => (await session.Settings.GetSettingsAsync(new Empty(),
            deadline: DateTime.UtcNow.AddSeconds(1))).IsAutoStart, "Auto start preference saved through Core");
        int languageUiId = ui.Id;
        await Task.Run(() => ApplicationUi.SelectComboIndex(ApplicationUi.Find(root, "Settings.General.Language"), 0, SupportedLanguages.Languages.Count));
        await AppProcessSession.WaitAsync(async () => (await session.Settings.GetSettingsAsync(new Empty(),
            deadline: DateTime.UtcNow.AddSeconds(1))).Language == "zh-CN", "Chinese language saved through Core");
        // The language control is re-enabled after SaveAsync completes. Wait for
        // it before reading JSON, so the observer cannot block an exclusive write.
        await AppProcessSession.WaitAsync(() => Task.Run(() =>
            ApplicationUi.Find(root, "Settings.Tab.General").Current.Name == "通用"
            && ApplicationUi.Find(root, "Settings.General.Language").Current.IsEnabled
            && ApplicationUi.SelectedLabel(ApplicationUi.Find(root, "Settings.General.Backdrop")) == "亚克力"),
            "Chinese labels updated in the existing UI");
        await AppProcessSession.WaitForJsonAsync(session.SettingsPath, json =>
            json["ApplicationTheme"]?.GetValue<string>() == nameof(AppTheme.Dark)
            && json["SystemBackdrop"]?.GetValue<string>() == nameof(AppSystemBackdrop.Acrylic)
            && json["IsAutoStart"]?.GetValue<bool>() == true && json["Language"]?.GetValue<string>() == "zh-CN");
        Check(!ui.HasExited && ui.Id == languageUiId, "Changing language restarted the UI process");
        await Task.Run(() => {
            ApplicationUi.Select(ApplicationUi.Find(root, "Settings.Tab.System"));
            ApplicationUi.Find(root, "Settings.System.ExportLogs", visible: true);
            ApplicationUi.Select(ApplicationUi.Find(root, "Settings.Tab.Others"));
            var logo = ApplicationUi.Find(root, "Settings.Others.AboutLogo", visible: true);
            Check(logo.Current.Name == "Virtual Paper", "About logo lost its accessible name");
            ApplicationUi.Select(ApplicationUi.Find(root, "Settings.Tab.General"));
            Check(Toggle(root).Current.ToggleState == ToggleState.On, "Returning to General lost the auto start setting");
            Check(ApplicationUi.SelectedLabel(ApplicationUi.Find(root, "Settings.General.Language")) == SupportedLanguages.Languages[0].Language,
                "Returning to General lost the language selection");
        });
        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        await Task.Run(() => {
            root = AutomationElement.FromHandle(ui.MainWindowHandle);
            ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Settings"));
            Check(ApplicationUi.Find(root, "Settings.Tab.General").Current.Name == "通用", "Reopened UI did not load Chinese resources");
            Check(ApplicationUi.SelectedLabel(ApplicationUi.Find(root, "Settings.General.Backdrop")) == "亚克力",
                "Reopened backdrop did not display the saved value in Chinese");
            Check(ApplicationUi.SelectedLabel(ApplicationUi.Find(root, "Settings.General.Language")) == SupportedLanguages.Languages[0].Language,
                "Reopened UI did not retain the Chinese language selection");
            Check(Toggle(root).Current.ToggleState == ToggleState.On, "Reopened UI did not retain auto start preference");
            Toggle(root).Toggle();
        });
        // Finish the preceding save before changing language again.
        await AppProcessSession.WaitForJsonAsync(session.SettingsPath, json =>
            json["IsAutoStart"]?.GetValue<bool>() == false && json["Language"]?.GetValue<string>() == "zh-CN");
        languageUiId = ui.Id;
        await Task.Run(() => ApplicationUi.SelectComboIndex(ApplicationUi.Find(root, "Settings.General.Language"), 1, SupportedLanguages.Languages.Count));
        await AppProcessSession.WaitAsync(() => Task.Run(() =>
            ApplicationUi.Find(root, "Settings.Tab.General").Current.Name == "General"
            && ApplicationUi.Find(root, "Settings.General.Language").Current.IsEnabled
            && ApplicationUi.SelectedLabel(ApplicationUi.Find(root, "Settings.General.Backdrop")) == "Acrylic"),
            "English labels updated in the existing UI");
        await AppProcessSession.WaitForJsonAsync(session.SettingsPath, json =>
            json["Language"]?.GetValue<string>() == "en-US" && json["IsAutoStart"]?.GetValue<bool>() == false);
        Check(!ui.HasExited && ui.Id == languageUiId, "Changing back to English restarted the UI process");
        await session.StopAsync();
        session.RecordEvidence(context);
        context.Evidence["Theme"] = nameof(AppTheme.Dark);
        context.Evidence["Backdrop"] = nameof(AppSystemBackdrop.Acrylic);
        context.Evidence["ChineseLanguageAndReopenedResourcesVerified"] = true;
        context.Evidence["CoreAutomaticallyRestartedUiForBackdrop"] = true;
        context.Evidence["LanguageUpdatedWithoutUiRestart"] = true;
        context.Evidence["AutoStartOsRegistrationTested"] = false;
        context.Evidence["BackdropPixelsAndAboutImageDecodeVerified"] = false;
    }

    private static TogglePattern Toggle(AutomationElement root) {
        var control = ApplicationUi.Find(root, "Settings.General.AutoStart");
        ApplicationUi.BringIntoView(control);
        return (TogglePattern)control.GetCurrentPattern(TogglePattern.Pattern);
    }

    private static AutomationElement OpenGeneral(nint window) {
        var root = AutomationElement.FromHandle(window);
        ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Settings"));
        ApplicationUi.Find(root, "Settings.General.Language");
        return root;
    }
}
