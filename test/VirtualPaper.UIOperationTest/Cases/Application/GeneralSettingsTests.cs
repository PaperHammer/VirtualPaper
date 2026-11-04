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
        await Task.Run(() => ApplicationUi.SelectComboIndex(ApplicationUi.Find(root, "Settings.General.Language"), 0, SupportedLanguages.Languages.Count, closesWindow: true));
        await AppProcessSession.WaitAsync(async () => (await session.Settings.GetSettingsAsync(new Empty(),
            deadline: DateTime.UtcNow.AddSeconds(1))).Language == "zh-CN", "Chinese language saved through Core");
        await AppProcessSession.WaitForJsonAsync(session.SettingsPath, json =>
            json["ApplicationTheme"]?.GetValue<string>() == nameof(AppTheme.Dark)
            && json["SystemBackdrop"]?.GetValue<string>() == nameof(AppSystemBackdrop.Acrylic)
            && json["IsAutoStart"]?.GetValue<bool>() == true && json["Language"]?.GetValue<string>() == "zh-CN");
        ui = await session.WaitForUiRestartAsync();
        root = await Task.Run(() => OpenGeneral(ui.MainWindowHandle));
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
        await AppProcessSession.WaitAsync(async () => !(await session.Settings.GetSettingsAsync(new Empty(),
            deadline: DateTime.UtcNow.AddSeconds(1))).IsAutoStart, "Auto start preference disabled again");
        await Task.Run(() => ApplicationUi.SelectComboIndex(ApplicationUi.Find(root, "Settings.General.Language"), 1, SupportedLanguages.Languages.Count, closesWindow: true));
        await AppProcessSession.WaitForJsonAsync(session.SettingsPath, json =>
            json["Language"]?.GetValue<string>() == "en-US" && json["IsAutoStart"]?.GetValue<bool>() == false);
        ui = await session.WaitForUiRestartAsync();
        await session.StopAsync();
        session.RecordEvidence(context);
        context.Evidence["Theme"] = nameof(AppTheme.Dark);
        context.Evidence["Backdrop"] = nameof(AppSystemBackdrop.Acrylic);
        context.Evidence["ChineseLanguageAndReopenedResourcesVerified"] = true;
        context.Evidence["CoreAutomaticallyRestartedUiForBackdropAndLanguage"] = true;
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
