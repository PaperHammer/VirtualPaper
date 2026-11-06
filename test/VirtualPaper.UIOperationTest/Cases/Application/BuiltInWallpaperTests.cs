using System.Diagnostics;
using System.Drawing;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Windows.Automation;
using Google.Protobuf.WellKnownTypes;
using GrpcDotNetNamedPipes;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Grpc.Service.WallpaperControl;
using VirtualPaper.Models.Cores;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class BuiltInWallpaperTests {
    private static string Payload => Path.Combine(Path.GetDirectoryName(
        Environment.GetEnvironmentVariable("VIRTUALPAPER_TEST_CORE_EXE"))!, "BuiltInWallpapers");

    private static WpBasicData[] ReadItems(string root) {
        Check(Directory.Exists(root), "Built-in wallpaper payload is missing: " + root);
        var items = Directory.GetFiles(root, Constants.Field.WpBasicDataFileName, SearchOption.AllDirectories)
            .Select(WpBasicData.Load).OrderBy(item => item.FolderName).ToArray();
        Check(items.Length > 0, "The built-in collection is empty.");
        return items;
    }

    public static async Task ImportMultipleAsync(UiTestContext context) {
        var bundled = ReadItems(Payload);
        string inputs = Path.Combine(context.Root, "multi-file inputs");
        Directory.CreateDirectory(inputs);
        var sources = new List<string>();
        var mediaHashes = new HashSet<string>();
        var webEntryHashes = new HashSet<string>();
        foreach (var item in bundled) {
            string path = Path.Combine(inputs, item.FolderName + (item.FType == FileType.FWebZip ? ".zip" : Path.GetExtension(item.FilePath)));
            if (item.FType == FileType.FWebZip) {
                using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
                foreach (string file in new[] { "index.html", "preview.gif", "project.json" })
                    archive.CreateEntryFromFile(Path.Combine(item.FolderPath, file), file);
                webEntryHashes.Add(Hash(item.FilePath));
            } else {
                File.Copy(item.FilePath, path);
                mediaHashes.Add(Hash(path));
            }
            sources.Add(path);
        }
        var inputHashes = sources.ToDictionary(path => path, Hash);
        var pickerSteps = new List<string>();
        context.Evidence["NativePickerSteps"] = pickerSteps;
        await using var session = new AppProcessSession();
        await session.StartAsync();
        var ui = await session.OpenUiAsync();
        var root = await Task.Run(() => {
            var window = AutomationElement.FromHandle(ui.MainWindowHandle);
            ApplicationUi.Select(ApplicationUi.Find(window, "App.Navigation.Library"));
            ApplicationUi.Invoke(ApplicationUi.Find(window, "Library.Import"));
            ApplicationUi.Invoke(ApplicationUi.Find(window, "Library.Import.Files"));
            NativeFileDialog.OpenFiles(ui, ui.MainWindowHandle, sources, pickerSteps.Add);
            return window;
        });
        string library = (await session.Settings.GetSettingsAsync(new Empty())).WallpaperDir;
        WpBasicData[] imported = [];
        await AppProcessSession.WaitAsync(() => {
            try {
                var files = Directory.GetFiles(library, Constants.Field.WpBasicDataFileName, SearchOption.AllDirectories);
                context.Evidence["ImportedMetadataCount"] = files.Length;
                if (files.Length != sources.Count) return Task.FromResult(false);
                imported = files.Select(WpBasicData.Load).ToArray();
                return Task.FromResult(imported.All(item => File.Exists(item.FilePath) && File.Exists(item.ThumbnailPath)));
            }
            catch (IOException) { return Task.FromResult(false); }
            catch (System.Text.Json.JsonException) { return Task.FromResult(false); }
        }, "One native picker submission imports every selected format", TimeSpan.FromSeconds(60));
        Check(imported.Select(item => item.WallpaperUid).Distinct().Count() == sources.Count, "Multi-import reused wallpaper identities.");
        Check(mediaHashes.SetEquals(imported.Where(item => item.FType != FileType.FWebZip).Select(item => Hash(item.FilePath))),
            "Multi-import omitted or changed an image, GIF or video.");
        Check(webEntryHashes.SetEquals(imported.Where(item => item.FType == FileType.FWebZip).Select(item => Hash(item.FilePath))), "Web import changed or lost an entry file.");
        Check(bundled.Select(item => item.FType).Order().SequenceEqual(imported.Select(item => item.FType).Order()), "Multi-import lost a media type.");
        await WallpaperLibraryTests.WaitForItemsAsync(root, sources.Count);
        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        await Task.Run(() => {
            root = AutomationElement.FromHandle(ui.MainWindowHandle);
            ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Library"));
            foreach (var item in imported) WallpaperLibraryTests.FindItem(root, item.WallpaperUid);
        });
        await WallpaperLibraryTests.WaitForItemsAsync(root, sources.Count);
        foreach (var (path, hash) in inputHashes) Check(Hash(path) == hash, "Import changed an original input: " + path);
        context.Evidence["ImportedTypes"] = imported.Select(item => item.FType.ToString()).ToArray();
        context.Evidence["OnePickerSubmissionImportedCount"] = imported.Length;
        context.Evidence["AllCardsAndOriginalInputsPreservedAfterReopen"] = true;
        await session.StopAsync();
        session.RecordEvidence(context);
    }

    public static async Task ApplyBuiltInsAsync(UiTestContext context) {
        await using var session = new AppProcessSession();
        string library = "";
        // Arrange an installed collection before Core/UI starts. Actual ISS deployment
        // and overwrite protection are covered independently by installer smoke.
        await session.StartAsync(settings => {
            library = settings.BuiltInWallpapersDir = Path.Combine(Path.GetDirectoryName(settings.WallpaperDir)!, "BuiltInWallpapers");
            FileUtil.CopyDirectory(Payload, library, true);
        });
        var items = ReadItems(library);
        var ui = await session.OpenUiAsync();
        var root = await Task.Run(() => {
            var window = AutomationElement.FromHandle(ui.MainWindowHandle);
            ApplicationUi.Select(ApplicationUi.Find(window, "App.Navigation.Library"));
            foreach (var item in items) WallpaperLibraryTests.FindItem(window, item.WallpaperUid);
            return window;
        });
        await WallpaperLibraryTests.WaitForItemsAsync(root, items.Length);
        var rpc = new Grpc_WallpaperControlService.Grpc_WallpaperControlServiceClient(new NamedPipeChannel(".", Constants.CoreField.GrpcPipeServerName));
        var evidence = new List<object>();
        context.Evidence["BuiltInPlayback"] = evidence;
        foreach (var item in items) {
            await ApplyAndClose(item, depth: false);
        }
        // A prepared cache must remain byte-for-byte intact when selected as 3D.
        var image = items.First(item => item.FType == FileType.FImage);
        var caches = Directory.GetFiles(image.FolderPath, "depth-*.png").ToDictionary(path => path, Hash);
        Check(caches.Count > 0, "The bundled image has no prepared depth map.");
        await ApplyAndClose(image, depth: true);
        Check(Directory.GetFiles(image.FolderPath, "depth-*.png").Length == caches.Count && caches.All(pair => Hash(pair.Key) == pair.Value),
            "Applying bundled 3D changed or replaced the prepared depth map.");
        context.Evidence["BuiltInPlayback"] = evidence;
        context.Evidence["PreparedDepthCachePreserved"] = true;
        await session.StopAsync();
        session.RecordEvidence(context);
        Check(((string[])context.Evidence["RuntimeErrors"]!).Length == 0, "Built-in playback logged runtime errors.");

        async Task ApplyAndClose(WpBasicData item, bool depth) {
            context.Evidence["CurrentWallpaper"] = item.Title + (depth ? " (3D)" : "");
            await new WallpaperFixture(ui, root, item).ApplyAsync(depth, usePointer: true);
            Process? player = null;
            await AppProcessSession.WaitAsync(() => {
                player ??= session.FindChild("VirtualPaper.PlayerWeb.exe");
                return Task.FromResult(player != null && !player.HasExited && DesktopPlayerWindow.Find(player.Id, out _) != 0);
            }, "Built-in player mounted on WorkerW: " + item.Title);
            using var owned = player!;
            var monitor = await rpc.GetRunMonitorByWallpaperAsync(new() { WpUid = item.WallpaperUid });
            string captures = Path.Combine(Constants.CommonPaths.LogDir, "UIOperation", "built-ins");
            Directory.CreateDirectory(captures);
            string first = Path.Combine(captures, item.FolderName + (depth ? "-3d" : "") + ".png");
            async Task Capture(string path) => await rpc.TakeScreenshotAsync(new() { MonitorId = monitor.DeviceId, SavePath = path },
                deadline: DateTime.UtcNow.AddSeconds(15));
            await AppProcessSession.WaitAsync(async () => {
                try {
                    await Capture(first);
                    using var frame = new Bitmap(first);
                    int min = 255, max = 0;
                    for (int y = 0; y < frame.Height; y += 16) for (int x = 0; x < frame.Width; x += 16) {
                        var color = frame.GetPixel(x, y);
                        min = Math.Min(min, Math.Min(color.R, Math.Min(color.G, color.B)));
                        max = Math.Max(max, Math.Max(color.R, Math.Max(color.G, color.B)));
                    }
                    return frame.Width >= 256 && frame.Height >= 144 && max - min > 20;
                }
                catch (global::Grpc.Core.RpcException) { return false; }
            }, "Built-in renders a nonblank frame: " + item.Title);
            double difference = 0;
            if (item.FType is FileType.FGif or FileType.FVideo or FileType.FWebZip) {
                await AppProcessSession.WaitAsync(async () => {
                    await Task.Delay(350);
                    string next = Path.Combine(captures, item.FolderName + "-moving.png");
                    await Capture(next);
                    return (difference = RenderingTests.Difference(first, next)) > .001;
                }, "Built-in animation advances: " + item.Title);
            }
            await Task.Run(() => {
                ApplicationUi.Invoke(ApplicationUi.Find(root, "Library.DisplayControls"));
                ApplicationUi.Invoke(ApplicationUi.Find(root, "Library.CloseWallpaper", visible: true));
                ApplicationUi.DismissFlyout(ui.MainWindowHandle);
                ApplicationUi.WaitUntilGone(root, "Library.CloseWallpaper");
            });
            await AppProcessSession.WaitForJsonAsync(Constants.CommonPaths.WallpaperLayoutPath, json => json.AsArray().Count == 0);
            await owned.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            // Player.Close intentionally terminates its process. Unlike Core/UI
            // shutdown, a zero player exit code is not part of this contract.
            evidence.Add(new { item.Title, Type = item.FType.ToString(), Depth = depth, PixelDifference = difference,
                PlayerId = owned.Id, PlayerExitCode = owned.ExitCode });
        }
    }

    private static string Hash(string path) {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
