using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Automation;
using Empty = Google.Protobuf.WellKnownTypes.Empty;
using VirtualPaper.Common;
using VirtualPaper.Models.Cores;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class WallpaperEffectsTests {
    public static async Task EditRestoreSaveAndReopenAsync(UiTestContext context) {
        string source = Path.Combine(context.Root, "wallpaper-effects.png");
        WallpaperLibraryTests.WriteImage(source);
        byte[] sourceHash = SHA256.HashData(await File.ReadAllBytesAsync(source));
        await using var session = new AppProcessSession();
        await session.StartAsync();
        context.Evidence["CoreProcessId"] = session.CoreId;
        var ui = await session.OpenUiAsync();
        nint mainWindow = ui.MainWindowHandle;
        var root = await Task.Run(() => {
            var window = AutomationElement.FromHandle(mainWindow);
            ApplicationUi.Select(ApplicationUi.Find(window, "App.Navigation.Library"));
            ApplicationUi.Invoke(ApplicationUi.Find(window, "Library.Import"));
            ApplicationUi.Invoke(ApplicationUi.Find(window, "Library.Import.Files"));
            NativeFileDialog.OpenFile(ui, mainWindow, source);
            return window;
        });
        string library = (await session.Settings.GetSettingsAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(1))).WallpaperDir;
        WpBasicData? data = null;
        await AppProcessSession.WaitAsync(async () => {
            var metadata = Directory.GetFiles(library, Constants.Field.WpBasicDataFileName, SearchOption.AllDirectories);
            if (metadata.Length != 1) return false;
            try {
                data = JsonSerializer.Deserialize(await File.ReadAllTextAsync(metadata[0]), WpBasicDataContext.Default.WpBasicData);
                return data != null && File.Exists(data.ThumbnailPath);
            }
            catch (IOException) { return false; }
            catch (JsonException) { return false; }
        }, "Effects fixture imported through UI");
        await Task.Run(() => {
            WallpaperLibraryTests.OpenItemMenu(mainWindow, root, data!.WallpaperUid, "Library.Item.Apply");
            ApplicationUi.Invoke(ApplicationUi.Find(root, "PrimaryButton", visible: true));
        });
        await AppProcessSession.WaitForJsonAsync(Constants.CommonPaths.WallpaperLayoutPath,
            json => json.AsArray().Count == 1 && json[0]?["FolderPath"]?.GetValue<string>() == data!.FolderPath);
        Process? player = null;
        await AppProcessSession.WaitAsync(() => {
            player ??= session.FindChild("VirtualPaper.PlayerWeb.exe");
            if (player == null) return Task.FromResult(false);
            Check(!player.HasExited, "Effects fixture player exited before desktop attachment");
            var hwnd = DesktopPlayerWindow.Find(player.Id, out var windows);
            context.Evidence["PlayerWindows"] = windows;
            return Task.FromResult(hwnd != 0);
        }, "Effects fixture player attached to desktop WorkerW");
        using var ownedPlayer = player!;
        string runtimePath = Directory.GetFiles(data!.FolderPath, Constants.Field.WpRuntimeDataFileName, SearchOption.AllDirectories).Single();
        var runtime = JsonNode.Parse(await File.ReadAllTextAsync(runtimePath))!;
        string effectPath = runtime["WpEffectFilePathUsing"]!.GetValue<string>();
        string templatePath = runtime["WpEffectFilePathTemplate"]!.GetValue<string>();
        string templateBefore = await File.ReadAllTextAsync(templatePath);
        var initial = JsonNode.Parse(await File.ReadAllTextAsync(effectPath))!;
        double initialIntensity = initial["AtmosphereIntensity"]!["Value"]!.GetValue<double>();
        string snow = string.Empty, style = string.Empty;

        await Task.Run(() => {
            var adjust = OpenAdjust(root, mainWindow, ui.Id);
            nint adjustWindow = adjust.Current.NativeWindowHandle;
            OpenAtmosphere(adjust);
            snow = ApplicationUi.SelectComboIndex(ApplicationUi.Find(adjust, "GeneralEffect.Atmosphere", visible: true),
                (int)AtmosphereEffect.Snow, Enum.GetValues<AtmosphereEffect>().Length);
            SetIntensity(adjust, 73);
            ApplicationUi.DismissFlyout(adjustWindow);
            ApplicationUi.WaitUntilGone(adjust, "GeneralEffect.AtmosphereIntensity");
            ApplicationUi.Invoke(ApplicationUi.Find(adjust, "GeneralEffect.Restore"));
            OpenAtmosphere(adjust);
            Check(Intensity(adjust).Current.Value == initialIntensity, "Restore did not recover the saved intensity");
            Check(ApplicationUi.SelectedLabel(ApplicationUi.Find(adjust, "GeneralEffect.Atmosphere", visible: true)) != snow,
                "Restore did not recover the saved atmosphere preset");
            ApplicationUi.SelectComboIndex(ApplicationUi.Find(adjust, "GeneralEffect.Atmosphere", visible: true),
                (int)AtmosphereEffect.Snow, Enum.GetValues<AtmosphereEffect>().Length);
            style = ApplicationUi.SelectComboIndex(ApplicationUi.Find(adjust, "GeneralEffect.AtmosphereStyle", visible: true), 1, 2);
            SetIntensity(adjust, 71);
            ApplicationUi.DismissFlyout(adjustWindow);
            ApplicationUi.WaitUntilGone(adjust, "GeneralEffect.AtmosphereIntensity");
            Check(JsonNode.DeepEquals(initial, JsonNode.Parse(File.ReadAllText(effectPath))), "Unsaved effects overwrote the applied configuration");
            ApplicationUi.Invoke(ApplicationUi.Find(adjust, "GeneralEffect.Apply"));
            ApplicationUi.WaitUntilGone(adjust, "GeneralEffect.Apply");
        });
        await AppProcessSession.WaitForJsonAsync(effectPath, json =>
            json["Atmosphere"]?["Value"]?.GetValue<int>() == (int)AtmosphereEffect.Snow
            && json["AtmosphereStyle"]?["Value"]?.GetValue<int>() == 1
            && json["AtmosphereIntensity"]?["Value"]?.GetValue<double>() == 71);
        Check(await File.ReadAllTextAsync(templatePath) == templateBefore, "Adjust overwrote the default effect template");
        Check(!ownedPlayer.HasExited, "Editing effects stopped the applied wallpaper player");
        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        mainWindow = ui.MainWindowHandle;
        await Task.Run(() => {
            root = AutomationElement.FromHandle(mainWindow);
            ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Library"));
            var adjust = OpenAdjust(root, mainWindow, ui.Id);
            OpenAtmosphere(adjust);
            Check(ApplicationUi.SelectedLabel(ApplicationUi.Find(adjust, "GeneralEffect.Atmosphere", visible: true)) == snow,
                "Reopened adjustment window did not load the saved atmosphere");
            Check(ApplicationUi.SelectedLabel(ApplicationUi.Find(adjust, "GeneralEffect.AtmosphereStyle", visible: true)) == style,
                "Reopened adjustment window did not load the saved style");
            Check(Intensity(adjust).Current.Value == 71, "Reopened adjustment window did not load the saved intensity");
            ApplicationUi.DismissFlyout(adjust.Current.NativeWindowHandle);
            ApplicationUi.WaitUntilGone(adjust, "GeneralEffect.AtmosphereIntensity");
            ApplicationUi.Invoke(ApplicationUi.Find(adjust, "GeneralEffect.Apply"));
            ApplicationUi.WaitUntilGone(adjust, "GeneralEffect.Apply");
            ApplicationUi.Invoke(ApplicationUi.Find(root, "Library.DisplayControls"));
            var close = ApplicationUi.Find(root, "Library.CloseWallpaper", visible: true);
            close.SetFocus();
            ApplicationUi.Invoke(close);
            ApplicationUi.DismissFlyout(mainWindow);
            ApplicationUi.WaitUntilGone(root, "Library.CloseWallpaper");
        });
        await AppProcessSession.WaitForJsonAsync(Constants.CommonPaths.WallpaperLayoutPath, json => json.AsArray().Count == 0);
        await ownedPlayer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Run(() => {
            WallpaperLibraryTests.OpenItemMenu(mainWindow, root, data.WallpaperUid, "Library.Item.Delete");
            ApplicationUi.Invoke(ApplicationUi.Find(root, "PrimaryButton", visible: true));
        });
        await AppProcessSession.WaitAsync(() => Task.FromResult(!Directory.Exists(data.FolderPath)), "Effects fixture deleted");
        Check(sourceHash.SequenceEqual(SHA256.HashData(await File.ReadAllBytesAsync(source))), "Effects editing modified the source image");
        await session.StopAsync();
        session.RecordEvidence(context);
        context.Evidence["PlayerProcessId"] = ownedPlayer.Id;
        context.Evidence["Atmosphere"] = snow;
        context.Evidence["Style"] = style;
        context.Evidence["Intensity"] = 71;
        context.Evidence["EditRestoreSaveAndReopenVerified"] = true;
        context.Evidence["RenderedPixelsVerified"] = false;
    }

    private static AutomationElement OpenAdjust(AutomationElement main, nint mainWindow, int uiId) {
        ApplicationUi.Invoke(ApplicationUi.Find(main, "Library.DisplayControls"));
        var button = ApplicationUi.Find(main, "Library.AdjustWallpaper", visible: true);
        button.SetFocus();
        ApplicationUi.Invoke(button);
        ApplicationUi.DismissFlyout(mainWindow);
        return ApplicationUi.FindWindow(uiId, "GeneralEffect.Apply");
    }

    private static void OpenAtmosphere(AutomationElement adjust) {
        var button = ApplicationUi.Find(adjust, "GeneralEffect.AtmosphereOptions");
        ApplicationUi.BringIntoView(button);
        ((ExpandCollapsePattern)button.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();
        ApplicationUi.Find(adjust, "GeneralEffect.Atmosphere", visible: true);
    }

    private static RangeValuePattern Intensity(AutomationElement adjust) => (RangeValuePattern)
        ApplicationUi.Find(adjust, "GeneralEffect.AtmosphereIntensity", visible: true, enabled: false).GetCurrentPattern(RangeValuePattern.Pattern);

    private static void SetIntensity(AutomationElement adjust, double value) {
        Intensity(adjust).SetValue(value);
        Check(Intensity(adjust).Current.Value == value, "Intensity slider did not accept the selected value");
    }
}
