using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Empty = Google.Protobuf.WellKnownTypes.Empty;
using VirtualPaper.Common;
using VirtualPaper.Models.Cores;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class WallpaperLibraryTests {
    public static async Task ImportApplyCloseDeleteAsync(UiTestContext context) {
        string source = Path.Combine(context.Root, "UI-operation-wallpaper.png");
        WriteImage(source);
        byte[] sourceHash = SHA256.HashData(await File.ReadAllBytesAsync(source));
        await using var session = new AppProcessSession();
        await session.StartAsync();
        context.Evidence["CoreProcessId"] = session.CoreId;
        var ui = await session.OpenUiAsync();
        nint ownerWindow = ui.MainWindowHandle;
        var root = await Task.Run(() => {
            var window = AutomationElement.FromHandle(ownerWindow);
            ApplicationUi.Select(ApplicationUi.Find(window, "App.Navigation.Library"));
            ApplicationUi.Invoke(ApplicationUi.Find(window, "Library.Import"));
            ApplicationUi.Invoke(ApplicationUi.Find(window, "Library.Import.Files"));
            NativeFileDialog.OpenFile(ui, ownerWindow, source);
            return window;
        });
        WpBasicData? imported = null;
        string library = (await session.Settings.GetSettingsAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(1))).WallpaperDir;
        await AppProcessSession.WaitAsync(async () => {
            var files = Directory.GetFiles(library, Constants.Field.WpBasicDataFileName, SearchOption.AllDirectories);
            if (files.Length != 1) return false;
            try {
                imported = JsonSerializer.Deserialize(await File.ReadAllTextAsync(files[0]), WpBasicDataContext.Default.WpBasicData);
                return imported != null && File.Exists(imported.ThumbnailPath);
            }
            catch (IOException) { return false; }
            catch (JsonException) { return false; }
        }, "UI import metadata and thumbnail");
        var data = imported!;
        Check(sourceHash.SequenceEqual(SHA256.HashData(await File.ReadAllBytesAsync(data.FilePath))), "Imported image differs from the source");
        await Task.Run(() => {
            FindItem(root, data.WallpaperUid);
            ApplicationUi.SetText(ApplicationUi.Find(root, "Library.Search"), "no-such-wallpaper");
        });
        await WaitForItemsAsync(root, 0);
        await Task.Run(() => ApplicationUi.SetText(ApplicationUi.Find(root, "Library.Search"), data.Title));
        await WaitForItemsAsync(root, 1);

        // Cancellation must leave both the actual library item and files intact.
        await Task.Run(() => {
            OpenItemMenu(ownerWindow, root, data.WallpaperUid, "Library.Item.Delete");
            ApplicationUi.Invoke(ApplicationUi.Find(root, "SecondaryButton", visible: true));
            ApplicationUi.WaitUntilGone(root, "SecondaryButton");
        });
        Check(Directory.Exists(data.FolderPath), "Cancel delete removed wallpaper files");
        await Task.Run(() => {
            OpenItemMenu(ownerWindow, root, data.WallpaperUid, "Library.Item.Apply");
            ApplicationUi.Invoke(ApplicationUi.Find(root, "PrimaryButton", visible: true)); // default: ordinary image
        });

        var layouts = await AppProcessSession.WaitForJsonAsync(Constants.CommonPaths.WallpaperLayoutPath,
            json => json.AsArray().Count == 1 && json[0]?["FolderPath"]?.GetValue<string>() == data.FolderPath);
        Check(layouts[0]!["RType"]!.GetValue<string>() == "RImage", "UI did not apply the ordinary image runtime");
        Process? player = null;
        nint desktopWindow = 0;
        await AppProcessSession.WaitAsync(() => {
            player ??= session.FindChild("VirtualPaper.PlayerWeb.exe");
            if (player == null) return Task.FromResult(false);
            context.Evidence["PlayerProcessId"] = player.Id;
            if (player.HasExited) throw new InvalidOperationException("Wallpaper player exited before desktop attachment");
            desktopWindow = DesktopPlayerWindow.Find(player.Id, out var playerWindows);
            context.Evidence["PlayerWindows"] = playerWindows;
            return Task.FromResult(desktopWindow != 0);
        }, "Owned player attached to desktop WorkerW");
        using var ownedPlayer = player!;
        int playerId = ownedPlayer.Id;
        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        ownerWindow = ui.MainWindowHandle;
        root = await Task.Run(() => {
            var window = AutomationElement.FromHandle(ownerWindow);
            ApplicationUi.Select(ApplicationUi.Find(window, "App.Navigation.Library"));
            FindItem(window, data.WallpaperUid);
            ApplicationUi.Invoke(ApplicationUi.Find(window, "Library.DisplayControls"));
            var close = ApplicationUi.Find(window, "Library.CloseWallpaper", visible: true);
            close.SetFocus();
            ApplicationUi.Invoke(close);
            ApplicationUi.DismissFlyout(ownerWindow);
            ApplicationUi.WaitUntilGone(window, "Library.CloseWallpaper");
            return window;
        });
        await AppProcessSession.WaitForJsonAsync(Constants.CommonPaths.WallpaperLayoutPath, json => json.AsArray().Count == 0);
        await ownedPlayer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Run(() => {
            OpenItemMenu(ownerWindow, root, data.WallpaperUid, "Library.Item.Delete");
            ApplicationUi.Invoke(ApplicationUi.Find(root, "PrimaryButton", visible: true));
        });
        await AppProcessSession.WaitAsync(() => Task.FromResult(!Directory.Exists(data.FolderPath)), "Confirmed deletion on disk");
        await WaitForItemsAsync(root, 0);
        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        await Task.Run(() => ApplicationUi.Select(ApplicationUi.Find(AutomationElement.FromHandle(ui.MainWindowHandle), "App.Navigation.Library")));
        var reopenedRoot = await Task.Run(() => AutomationElement.FromHandle(ui.MainWindowHandle));
        await WaitForItemsAsync(reopenedRoot, 0);
        Check(sourceHash.SequenceEqual(SHA256.HashData(await File.ReadAllBytesAsync(source))), "Library deletion modified the input file");
        await session.StopAsync();
        session.RecordEvidence(context);
        context.Evidence["PlayerProcessId"] = playerId;
        context.Evidence["DesktopWindowHandle"] = desktopWindow.ToInt64();
        context.Evidence["PlayerExitedAfterClose"] = ownedPlayer.HasExited;
        context.Evidence["ImportSearchApplyReopenCloseCancelDeleteAndDeleteVerified"] = true;
    }

    private static AutomationElement FindItem(AutomationElement root, string uid) {
        AutomationElement? item = null;
        var timer = Stopwatch.StartNew();
        while (item == null && timer.Elapsed < TimeSpan.FromSeconds(10)) {
            item = ApplicationUi.Find(root, "Library.Items").FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "Library.Item." + uid));
            if (item == null) Thread.Sleep(100);
        }
        if (item != null) return item;
        var children = ApplicationUi.Find(root, "Library.Items").FindAll(TreeScope.Descendants, Condition.TrueCondition)
            .Cast<AutomationElement>().Select(element => $"{element.Current.ControlType.ProgrammaticName} {element.Current.AutomationId} {element.Current.Name}");
        throw new TimeoutException($"Wallpaper card not found: {uid}\n{string.Join("\n", children)}");
    }

    private static void OpenItemMenu(nint ownerWindow, AutomationElement root, string uid, string action) {
        using var foreground = ApplicationUi.Foreground(ownerWindow);
        ApplicationUi.OpenContextMenu(ownerWindow, FindItem(root, uid));
        try { ApplicationUi.Invoke(ApplicationUi.Find(root, action, visible: true)); }
        catch {
            WindowSnapshot.Save(ownerWindow, Path.Combine(Constants.CommonPaths.LogDir, "UIOperation", "wallpaper-menu.png"));
            throw;
        }
    }

    private static Task WaitForItemsAsync(AutomationElement root, int count) => AppProcessSession.WaitAsync(() => Task.Run(() =>
        ApplicationUi.Find(root, "Library.Items").FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)).Count == count), $"Library displays {count} items");

    private static void WriteImage(string path) {
        byte[] pixels = new byte[256 * 144 * 4];
        for (int i = 0; i < pixels.Length; i += 4) {
            pixels[i] = (byte)(i / 4 % 256);
            pixels[i + 1] = 90;
            pixels[i + 2] = 160;
            pixels[i + 3] = 255;
        }
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(256, 144, 96, 96, PixelFormats.Bgra32, null, pixels, 256 * 4)));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
