using System.Drawing;
using System.Windows.Automation;
using Google.Protobuf.WellKnownTypes;
using VirtualPaper.Common;
using VirtualPaper.UIOperationTest.Infrastructure;
using Windows.UI.ViewManagement;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class ThemeSyncTests {
    public static async Task MainWindowAndTrayAsync(UiTestContext context) {
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
            return window;
        });
        string folder = Path.Combine(Constants.CommonPaths.LogDir, "UIOperation", "theme-sync");
        var evidence = new List<object>();
        var themes = new[] { AppTheme.Light, AppTheme.Dark, AppTheme.Auto, AppTheme.Light, AppTheme.Dark };
        for (int index = 0; index < themes.Length; index++) {
            AppTheme theme = themes[index];
            if (index != 0) await Task.Run(() => ApplicationUi.Invoke(ApplicationUi.Find(root, "App.Theme")));
            await AppProcessSession.WaitAsync(async () => (int)(await session.Settings.GetSettingsAsync(new Empty(),
                deadline: DateTime.UtcNow.AddSeconds(1))).ApplicationTheme == (int)theme, "Theme preference " + theme);
            await AppProcessSession.WaitForJsonAsync(session.SettingsPath,
                json => json["ApplicationTheme"]?.GetValue<string>() == theme.ToString());
            bool dark = theme == AppTheme.Dark || theme == AppTheme.Auto
                && new UISettings().GetColorValue(UIColorType.Background).R < 128;
            double main = await CaptureThemeAsync(ui.MainWindowHandle, dark, Path.Combine(folder, $"{index}-{theme}-main.png"));
            var menu = await Task.Run(() => TrayUi.Open(session.CoreId));
            double tray;
            try { tray = await CaptureThemeAsync(menu.Window, dark, Path.Combine(folder, $"{index}-{theme}-tray.png")); }
            finally { await Task.Run(() => TrayUi.Dismiss(menu)); }
            evidence.Add(new { Preference = theme.ToString(), Resolved = dark ? "Dark" : "Light", MainLuminance = main, TrayLuminance = tray });

            if (index is 0 or 1) {
                await Task.Run(() => {
                    ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Intelligent"));
                    var add = ApplicationUi.Find(root, "Intelligent.AddTask", visible: true);
                    Check(!string.IsNullOrWhiteSpace(add.Current.Name), "New task button has no accessible name");
                    Check(add.Current.IsKeyboardFocusable && add.Current.BoundingRectangle.Width >= 44,
                        "New task button is not keyboard accessible or has a small hit target");
                    ApplicationUi.Invoke(add);
                    ApplicationUi.Invoke(ApplicationUi.Find(root, "Intelligent.Task.Cancel", visible: true));
                    ApplicationUi.WaitUntilGone(root, "Intelligent.Task.Cancel");
                    WindowSnapshot.Save(ui.MainWindowHandle, Path.Combine(folder, $"{theme}-intelligent.png"));
                    ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Settings"));
                });
            }
        }
        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        await Task.Run(() => {
            root = AutomationElement.FromHandle(ui.MainWindowHandle);
            ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Settings"));
        });
        await CaptureThemeAsync(ui.MainWindowHandle, true, Path.Combine(folder, "reopened-dark-main.png"));
        var reopenedMenu = await Task.Run(() => TrayUi.Open(session.CoreId));
        try { await CaptureThemeAsync(reopenedMenu.Window, true, Path.Combine(folder, "reopened-dark-tray.png")); }
        finally { await Task.Run(() => TrayUi.Dismiss(reopenedMenu)); }
        await session.StopAsync();
        session.RecordEvidence(context);
        Check(((string[])context.Evidence["RuntimeErrors"]!).Length == 0, "Theme synchronization logged a runtime error");
        context.Evidence["RenderedMainAndTrayThemes"] = evidence;
        context.Evidence["ReopenedDarkThemeVerified"] = true;
        context.Evidence["IntelligentButtonInLightAndDarkVerified"] = true;
        context.Evidence["SystemThemeChanged"] = false;
    }

    private static async Task<double> CaptureThemeAsync(nint window, bool dark, string path) {
        double median = 0;
        await AppProcessSession.WaitAsync(() => Task.Run(() => {
            WindowSnapshot.Save(window, path);
            Check(File.Exists(path), "Could not capture rendered theme: " + path);
            using var bitmap = new Bitmap(path);
            var values = new List<double>();
            // Ignore the title bar, navigation, popup shadow and rounded corners.
            for (int y = bitmap.Height / 5; y < bitmap.Height * 4 / 5; y += 5)
                for (int x = bitmap.Width / 3; x < bitmap.Width * 4 / 5; x += 5) {
                    var pixel = bitmap.GetPixel(x, y);
                    values.Add(pixel.R * 0.2126 + pixel.G * 0.7152 + pixel.B * 0.0722);
                }
            Check(values.Count > 0, "Theme capture has no usable pixels");
            values.Sort();
            median = values[values.Count / 2];
            return dark ? median < 100 : median > 170;
        }), $"Rendered {(dark ? "dark" : "light")} theme: {path}");
        return median;
    }
}
