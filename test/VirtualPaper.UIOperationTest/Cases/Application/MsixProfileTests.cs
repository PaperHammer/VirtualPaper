using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Windows.Automation;
using Google.Protobuf.WellKnownTypes;
using VirtualPaper.Common;
using VirtualPaper.Models.Cores;
using VirtualPaper.TestSupport;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class MsixProfileTests {
    private static string Baseline => Path.Combine(Constants.CommonPaths.AppDataDir, "msix-profile-baseline.json");

    public static Task PrepareAsync(UiTestContext context) => RunAsync(context, prepare: true);
    public static Task VerifyAsync(UiTestContext context) => RunAsync(context, prepare: false);

    private static async Task RunAsync(UiTestContext context, bool prepare) {
        Check(PackageActivation.ExpectedFullName != null, "This case requires an installed MSIX package.");
        JsonNode? baseline = prepare ? null : JsonNode.Parse(await File.ReadAllTextAsync(Baseline));
        await using var session = new AppProcessSession();
        await session.StartAsync(preserveProfile: !prepare, freshProfile: prepare);
        context.Evidence["CoreProcessId"] = session.CoreId;
        context.Evidence["PackageFullName"] = PackageActivation.ExpectedFullName;
        context.Evidence["PackageActivation"] = "AUMID";
        // A fresh package must open its UI itself; ShowUI cannot hide a broken first launch.
        var ui = prepare ? await session.WaitForUiAsync(firstRun: true) : await session.OpenUiAsync();
        var settings = await session.Settings.GetSettingsAsync(new Empty());
        if (!prepare) {
            Check(settings.Language == baseline!["Language"]!.GetValue<string>(), "Reinstall changed the saved language.");
            Check(settings.WallpaperDir == baseline["WallpaperDir"]!.GetValue<string>(), "Reinstall changed the saved library.");
        }
        bool expectedNavigation = prepare || baseline!["NavigationAnimationsEnabled"]!.GetValue<bool>();
        string expectedTab = LocalizedTestStrings.Read(AppContext.BaseDirectory,
            settings.Language, "AppSettings_SelBarItem1_General");
        await Task.Run(() => {
            var window = AutomationElement.FromHandle(ui.MainWindowHandle);
            ApplicationUi.Select(ApplicationUi.Find(window, "App.Navigation.Settings"));
            Check(ApplicationUi.Find(window, "Settings.Tab.General").Current.Name ==
                expectedTab, "The installed first-run/profile UI is not localized.");
            ApplicationUi.Select(ApplicationUi.Find(window, "Settings.Tab.Performance"));
            var control = ApplicationUi.Find(window, "Settings.Performance.InterfaceAnimations");
            ApplicationUi.BringIntoView(control);
            var toggle = (TogglePattern)control.GetCurrentPattern(TogglePattern.Pattern);
            Check(toggle.Current.ToggleState == (expectedNavigation ? ToggleState.On : ToggleState.Off),
                "The real UI did not retain its navigation preference.");
            if (prepare) toggle.Toggle();
        });
        if (prepare) {
            await AppProcessSession.WaitAsync(async () => !(await session.Settings.GetSettingsAsync(new Empty(),
                deadline: DateTime.UtcNow.AddSeconds(1))).NavigationAnimationsEnabled, "Navigation preference reached Core");
            await AppProcessSession.WaitForJsonAsync(session.SettingsPath,
                saved => saved["NavigationAnimationsEnabled"]?.GetValue<bool>() == false);
            // Retain a real imported card across package reinstallation.
            string image = Path.Combine(Constants.CommonPaths.AppDataDir, "retained-import.png");
            WallpaperLibraryTests.WriteImage(image);
            await Task.Run(() => {
                var window = AutomationElement.FromHandle(ui.MainWindowHandle);
                ApplicationUi.Select(ApplicationUi.Find(window, "App.Navigation.Library"));
                ApplicationUi.Invoke(ApplicationUi.Find(window, "Library.Import"));
                ApplicationUi.Invoke(ApplicationUi.Find(window, "Library.Import.Files"));
                NativeFileDialog.OpenFile(ui, ui.MainWindowHandle, image);
            });
            await AppProcessSession.WaitAsync(() => Task.FromResult(
                Directory.GetFiles(settings.WallpaperDir, Constants.Field.WpBasicDataFileName, SearchOption.AllDirectories).Length == 1),
                "First-run MSIX imported a retained wallpaper");
        } else {
            var item = WpBasicData.Load(Directory.GetFiles(settings.WallpaperDir,
                Constants.Field.WpBasicDataFileName, SearchOption.AllDirectories).Single());
            Check(item.WallpaperUid == baseline!["WallpaperUid"]!.GetValue<string>() && File.Exists(item.FilePath),
                "Reinstall lost the imported wallpaper.");
            Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(item.FilePath))) == baseline["MediaSha256"]!.GetValue<string>(),
                "Reinstall changed the imported media.");
            await Task.Run(() => {
                var window = AutomationElement.FromHandle(ui.MainWindowHandle);
                ApplicationUi.Select(ApplicationUi.Find(window, "App.Navigation.Library"));
                WallpaperLibraryTests.FindItem(window, item.WallpaperUid);
            });
        }
        await session.StopAsync();
        if (prepare) {
            baseline = JsonNode.Parse(await File.ReadAllTextAsync(session.SettingsPath))!;
            var item = WpBasicData.Load(Directory.GetFiles(settings.WallpaperDir,
                Constants.Field.WpBasicDataFileName, SearchOption.AllDirectories).Single());
            baseline["WallpaperUid"] = item.WallpaperUid;
            baseline["MediaSha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(item.FilePath)));
            await File.WriteAllTextAsync(Baseline, baseline.ToJsonString());
        }
        context.Evidence["FirstRunAutomaticUiVerified"] = prepare;
        context.Evidence["RetainedProfileVerified"] = !prepare;
        context.Evidence["Language"] = settings.Language;
        session.RecordEvidence(context);
    }
}
