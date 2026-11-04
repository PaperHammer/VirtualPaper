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

internal static class ScreenFitTests {
    public static async Task PreviewCancelApplyAndReopenAsync(UiTestContext context) {
        string source = Path.Combine(context.Root, "screen-fit-portrait.png");
        WallpaperLibraryTests.WriteImage(source, 256, 384);
        byte[] sourceHash = SHA256.HashData(await File.ReadAllBytesAsync(source));
        await using var session = new AppProcessSession();
        await session.StartAsync();
        context.Evidence["CoreProcessId"] = session.CoreId;
        string library = (await session.Settings.GetSettingsAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(1))).WallpaperDir;
        var ui = await session.OpenUiAsync();
        nint ownerWindow = ui.MainWindowHandle;
        var root = await Task.Run(() => {
            var window = AutomationElement.FromHandle(ownerWindow);
            ApplicationUi.Select(ApplicationUi.Find(window, "App.Navigation.Library"));
            return window;
        });
        string selectedMonitor = string.Empty, containPlan = string.Empty, coverPlan = string.Empty;
        await Task.Run(() => {
            ApplicationUi.Invoke(ApplicationUi.Find(root, "Library.ScreenFit"));
            ApplicationUi.Find(root, "ScreenFit.SelectImage", visible: true);
            Check(!Primary(root).Current.IsEnabled, "Screen fit allowed applying without a selected image");
            SelectImage(ui, ownerWindow, root, source);
            selectedMonitor = ApplicationUi.SelectedLabel(ApplicationUi.Find(root, "ScreenFit.Monitor"));
            Check(!string.IsNullOrWhiteSpace(selectedMonitor), "Screen fit did not select a monitor");
            ApplicationUi.SelectComboIndex(ApplicationUi.Find(root, "ScreenFit.Mode"), 0, 2);
            containPlan = ApplicationUi.Find(root, "ScreenFit.Plan", visible: true).Current.Name;
            ApplicationUi.SelectComboIndex(ApplicationUi.Find(root, "ScreenFit.Mode"), 1, 2);
            coverPlan = ApplicationUi.Find(root, "ScreenFit.Plan", visible: true).Current.Name;
            Check(containPlan != coverPlan, "Contain and cover did not update the visible image area");
            ApplicationUi.Invoke(ApplicationUi.Find(root, "CloseButton", visible: true));
            ApplicationUi.WaitUntilGone(root, "ScreenFit.SelectImage");
        });
        Check(Directory.GetFiles(library, Constants.Field.WpBasicDataFileName, SearchOption.AllDirectories).Length == 0,
            "Cancel screen fit imported a wallpaper");
        Check(session.FindChild("VirtualPaper.PlayerWeb.exe") == null, "Cancel screen fit started a player");
        await AppProcessSession.WaitForJsonAsync(Constants.CommonPaths.WallpaperLayoutPath, json => json.AsArray().Count == 0);

        await Task.Run(() => {
            ApplicationUi.Invoke(ApplicationUi.Find(root, "Library.ScreenFit"));
            ApplicationUi.Find(root, "ScreenFit.SelectImage", visible: true);
            Check(!Primary(root).Current.IsEnabled, "Reopened screen fit retained a cancelled source");
            SelectImage(ui, ownerWindow, root, source);
            ApplicationUi.SelectComboIndex(ApplicationUi.Find(root, "ScreenFit.Mode"), 0, 2);
            Check(ApplicationUi.Find(root, "ScreenFit.Plan", visible: true).Current.Name == containPlan,
                "Reopened screen fit calculated a different contain plan");
            ApplicationUi.Invoke(Primary(root));
            ApplicationUi.WaitUntilGone(root, "ScreenFit.SelectImage");
        });
        var layout = await AppProcessSession.WaitForJsonAsync(Constants.CommonPaths.WallpaperLayoutPath,
            json => json.AsArray().Count == 1);
        Check(layout[0]!["RType"]!.GetValue<string>() == "RImage", "Screen fit applied an unexpected runtime");
        string folder = layout[0]!["FolderPath"]!.GetValue<string>();
        Check(selectedMonitor.StartsWith(layout[0]!["MonitorContent"]!.GetValue<string>() + " · ", StringComparison.Ordinal),
            "Screen fit applied to a different monitor");
        var data = JsonSerializer.Deserialize(await File.ReadAllTextAsync(Path.Combine(folder, Constants.Field.WpBasicDataFileName)),
            WpBasicDataContext.Default.WpBasicData)!;
        Check(sourceHash.SequenceEqual(SHA256.HashData(await File.ReadAllBytesAsync(data.FilePath))),
            "Screen fit changed the source pixels despite enhancement being disabled");
        var runtimePath = Directory.GetFiles(library, Constants.Field.WpRuntimeDataFileName, SearchOption.AllDirectories)
            .Single(path => Path.GetDirectoryName(path)!.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        var runtime = JsonNode.Parse(await File.ReadAllTextAsync(runtimePath))!;
        string[] effectPaths = [runtime["WpEffectFilePathUsing"]!.GetValue<string>(),
            runtime["WpEffectFilePathTemplate"]!.GetValue<string>(), runtime["WpEffectFilePathTemporary"]!.GetValue<string>()];
        foreach (string path in effectPaths) await AssertContainAsync(path);

        Process? player = null;
        nint desktopWindow = 0;
        await AppProcessSession.WaitAsync(() => {
            player ??= session.FindChild("VirtualPaper.PlayerWeb.exe");
            if (player == null) return Task.FromResult(false);
            Check(!player.HasExited, "Screen fit player exited before desktop attachment");
            desktopWindow = DesktopPlayerWindow.Find(player.Id, out var windows);
            context.Evidence["PlayerWindows"] = windows;
            return Task.FromResult(desktopWindow != 0);
        }, "Screen fit player attached to desktop WorkerW");
        using var ownedPlayer = player!;
        context.Evidence["PlayerProcessId"] = ownedPlayer.Id;
        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        ownerWindow = ui.MainWindowHandle;
        await Task.Run(() => {
            root = AutomationElement.FromHandle(ownerWindow);
            ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Library"));
            WallpaperLibraryTests.FindItem(root, data.WallpaperUid);
            ApplicationUi.Invoke(ApplicationUi.Find(root, "Library.ScreenFit"));
            ApplicationUi.Find(root, "ScreenFit.SelectImage", visible: true);
            Check(!Primary(root).Current.IsEnabled, "Current wallpaper preview incorrectly enabled applying without a source");
            ApplicationUi.Find(root, "ScreenFit.Preview", visible: true);
            ApplicationUi.WaitUntilGone(root, "ScreenFit.Sample");
            ApplicationUi.Invoke(ApplicationUi.Find(root, "CloseButton", visible: true));
            ApplicationUi.WaitUntilGone(root, "ScreenFit.SelectImage");
            ApplicationUi.Invoke(ApplicationUi.Find(root, "Library.DisplayControls"));
            var close = ApplicationUi.Find(root, "Library.CloseWallpaper", visible: true);
            close.SetFocus();
            ApplicationUi.Invoke(close);
            ApplicationUi.DismissFlyout(ownerWindow);
            ApplicationUi.WaitUntilGone(root, "Library.CloseWallpaper");
        });
        foreach (string path in effectPaths) await AssertContainAsync(path);
        await AppProcessSession.WaitForJsonAsync(Constants.CommonPaths.WallpaperLayoutPath, json => json.AsArray().Count == 0);
        await ownedPlayer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Run(() => {
            WallpaperLibraryTests.OpenItemMenu(ownerWindow, root, data.WallpaperUid, "Library.Item.Delete");
            ApplicationUi.Invoke(ApplicationUi.Find(root, "PrimaryButton", visible: true));
        });
        await AppProcessSession.WaitAsync(() => Task.FromResult(!Directory.Exists(folder)), "Screen fit fixture deleted");
        Check(sourceHash.SequenceEqual(SHA256.HashData(await File.ReadAllBytesAsync(source))), "Screen fit modified the input file");
        await session.StopAsync();
        session.RecordEvidence(context);
        context.Evidence["SelectedMonitor"] = selectedMonitor;
        context.Evidence["ContainPlan"] = containPlan;
        context.Evidence["CoverPlan"] = coverPlan;
        context.Evidence["DesktopWindowHandle"] = desktopWindow.ToInt64();
        context.Evidence["PreviewCancelApplyAndReopenVerified"] = true;
        context.Evidence["SuperResolutionInferenceTested"] = false;
    }

    private static AutomationElement Primary(AutomationElement root) => root.FindFirst(TreeScope.Descendants,
        new PropertyCondition(AutomationElement.AutomationIdProperty, "PrimaryButton"))
        ?? throw new InvalidOperationException("Screen fit primary action not found");

    private static void SelectImage(Process ui, nint ownerWindow, AutomationElement root, string source) {
        ApplicationUi.Invoke(ApplicationUi.Find(root, "ScreenFit.SelectImage", visible: true));
        NativeFileDialog.OpenFile(ui, ownerWindow, source);
        var enhancement = ApplicationUi.Find(root, "ScreenFit.Enhance");
        Check(((TogglePattern)enhancement.GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState == ToggleState.Off,
            "Screen fit enabled enhancement without user selection");
        ApplicationUi.Find(root, "ScreenFit.Preview", visible: true);
        ApplicationUi.WaitUntilGone(root, "ScreenFit.Sample");
        Check(Primary(root).Current.IsEnabled, "Screen fit did not enable applying a valid image");
    }

    private static async Task AssertContainAsync(string path) {
        await AppProcessSession.WaitForJsonAsync(path,
            json => json["Scaling"]?["Value"]?.GetValue<int>() == (int)ObjectFit.Contain);
    }
}
