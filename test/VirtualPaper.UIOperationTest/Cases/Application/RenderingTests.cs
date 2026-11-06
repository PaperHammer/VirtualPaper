using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Windows.Automation;
using GrpcDotNetNamedPipes;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.Grpc.Service.WallpaperControl;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class RenderingTests {
    public static async Task RunAsync(UiTestContext context) {
        string core = Environment.GetEnvironmentVariable("VIRTUALPAPER_TEST_CORE_EXE")!;
        string model = Path.Combine(Path.GetDirectoryName(core)!, Constants.WorkingDir.ML_DepthEstimate_AI_Models,
            "depth_anything_v2_vits_dynamic.onnx");
        Check(File.Exists(model), "Real depth model must already be installed: " + model);
        string source = Path.Combine(context.Root, "render-fixture.png");
        WriteScene(source);
        await using var session = new AppProcessSession();
        await session.StartAsync();
        context.Evidence["CoreProcessId"] = session.CoreId;
        var fixture = await WallpaperFixture.ImportAsync(session, source);
        await fixture.ApplyAsync(depth: true);
        await AppProcessSession.WaitForJsonAsync(Constants.CommonPaths.WallpaperLayoutPath,
            json => json.AsArray().Count == 1 && json[0]?["RType"]?.GetValue<string>() == "RImage3D");
        string runtimePath = Directory.GetFiles(fixture.Data.FolderPath, Constants.Field.WpRuntimeDataFileName, SearchOption.AllDirectories).Single();
        using var runtime = JsonDocument.Parse(await File.ReadAllTextAsync(runtimePath));
        string storedDepthPath = runtime.RootElement.GetProperty("DepthFilePath").GetString()!;
        Check(!Path.IsPathRooted(storedDepthPath), "The installed depth metadata contains an absolute path");
        string depthPath = Path.GetFullPath(storedDepthPath, Path.GetDirectoryName(runtimePath)!);
        Check(File.Exists(depthPath), "Actual depth model did not produce its cache");
        using (var depth = new Bitmap(depthPath)) {
            Check(depth.Width > 0 && depth.Height > 0, "Depth cache is not a readable image");
            context.Evidence["DepthDimensions"] = $"{depth.Width}x{depth.Height}";
        }
        System.Diagnostics.Process? player = null;
        await AppProcessSession.WaitAsync(() => {
            player ??= session.FindChild("VirtualPaper.PlayerWeb.exe");
            return Task.FromResult(player != null && !player.HasExited && DesktopPlayerWindow.Find(player.Id, out _) != 0);
        }, "Real 3D player desktop attachment");
        using var owned = player!;
        context.Evidence["PlayerProcessId"] = owned.Id;
        await Task.Run(() => {
            using var foreground = ApplicationUi.Foreground(fixture.Ui.MainWindowHandle);
            fixture.Root.SetFocus();
            ApplicationUi.Invoke(ApplicationUi.Find(fixture.Root, "Library.DisplayControls"));
            var adjustButton = ApplicationUi.Find(fixture.Root, "Library.AdjustWallpaper", visible: true);
            adjustButton.SetFocus();
            ApplicationUi.Invoke(adjustButton);
            ApplicationUi.DismissFlyout(fixture.Ui.MainWindowHandle);
            var adjust = ApplicationUi.FindWindow(fixture.Ui.Id, "GeneralEffect.Apply");
            var options = ApplicationUi.Find(adjust, "GeneralEffect.ParallaxOptions");
            ApplicationUi.BringIntoView(options);
            ((ExpandCollapsePattern)options.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();
            var auto = (TogglePattern)ApplicationUi.Find(adjust, "GeneralEffect.ParallaxAutoDrift", visible: true).GetCurrentPattern(TogglePattern.Pattern);
            if (auto.Current.ToggleState != ToggleState.On) auto.Toggle();
            ApplicationUi.DismissFlyout(adjust.Current.NativeWindowHandle);
            ApplicationUi.Invoke(ApplicationUi.Find(adjust, "GeneralEffect.Apply"));
            ApplicationUi.WaitUntilGone(adjust, "GeneralEffect.Apply");
        });
        var rpc = new Grpc_WallpaperControlService.Grpc_WallpaperControlServiceClient(new NamedPipeChannel(".", Constants.CoreField.GrpcPipeServerName));
        var monitor = await rpc.GetRunMonitorByWallpaperAsync(new Grpc_GetMonitorRequest { WpUid = fixture.Data.WallpaperUid });
        Check(!string.IsNullOrEmpty(monitor.DeviceId), "Applied 3D wallpaper has no monitor");
        string folder = Path.Combine(Constants.CommonPaths.LogDir, "UIOperation", "rendering");
        Directory.CreateDirectory(folder);
        async Task<string> Capture(string name) {
            string path = Path.Combine(folder, name + ".png");
            await rpc.TakeScreenshotAsync(new Grpc_WallpaperScreenshotRequest { MonitorId = monitor.DeviceId, SavePath = path },
                deadline: DateTime.UtcNow.AddSeconds(15));
            Check(File.Exists(path) && new FileInfo(path).Length > 1000, "Screenshot RPC produced no rendered image");
            return path;
        }
        async Task Send(IpcMessage message) => await rpc.SendMessageWallpaperAsync(new Grpc_WallpaperMessageRequest {
            MonitorId = monitor.DeviceId, Msg = JsonSerializer.Serialize(message, IpcMessageContext.Default.IpcMessage)
        });
        string first = string.Empty;
        await AppProcessSession.WaitAsync(async () => {
            first = await Capture("01-running");
            using var frame = new Bitmap(first);
            var samples = new[] { (.9, .1, Color.SkyBlue), (.73, .3, Color.ForestGreen), (.28, .5, Color.DarkSlateBlue) };
            return samples.All(sample => {
                var actual = frame.GetPixel((int)(sample.Item1 * frame.Width), (int)(sample.Item2 * frame.Height));
                return Math.Abs(actual.R - sample.Item3.R) + Math.Abs(actual.G - sample.Item3.G) + Math.Abs(actual.B - sample.Item3.B) < 45;
            });
        }, "Rendered scene preserves the fixture's sky, mountain and tree colors");
        double moving = 0;
        await AppProcessSession.WaitAsync(async () => {
            await Task.Delay(300);
            return (moving = Difference(first, await Capture("02-moving"))) > .2;
        }, "3D auto drift visibly changes rendered pixels");
        await Send(new VirtualPaperSuspendCmd());
        await Task.Delay(400);
        string paused = await Capture("03-paused");
        await Task.Delay(900);
        double stopped = Difference(paused, await Capture("04-still-paused"));
        Check(stopped < .02, $"Paused 3D scene continued moving: {stopped}");
        await Send(new VirtualPaperResumeCmd());
        double resumed = 0;
        await AppProcessSession.WaitAsync(async () => {
            await Task.Delay(300);
            return (resumed = Difference(paused, await Capture("05-resumed"))) > .2;
        }, "3D rendering resumes through the production playback hook");
        context.Evidence["RenderedPixelDifference"] = new { moving, stopped, resumed };
        context.Evidence["RenderArtifacts"] = "logs/UIOperation/rendering";
        await session.StopAsync();
        session.RecordEvidence(context);
        Check(((string[])context.Evidence["RuntimeErrors"]!).Length == 0, "Rendering logged runtime errors");
        context.Evidence["RealDepthInferenceRenderPauseResumeVerified"] = true;
    }

    private static double Difference(string left, string right) {
        using var a = new Bitmap(left); using var b = new Bitmap(right);
        Check(a.Size == b.Size && a.Width >= 256 && a.Height >= 144, "Invalid rendered dimensions");
        long difference = 0; int count = 0;
        for (int y = 0; y < a.Height; y += 8) for (int x = 0; x < a.Width; x += 8) {
            Color c = a.GetPixel(x, y), d = b.GetPixel(x, y);
            difference += Math.Abs(c.R - d.R) + Math.Abs(c.G - d.G) + Math.Abs(c.B - d.B);
            count += 3;
        }
        return (double)difference / count;
    }

    internal static void WriteScene(string path) {
        using var image = new Bitmap(640, 360); using var g = Graphics.FromImage(image);
        g.Clear(Color.SkyBlue);
        g.FillRectangle(Brushes.DarkSeaGreen, 0, 220, 640, 140);
        for (int x = 0; x < 640; x += 40) g.FillRectangle(Brushes.SeaGreen, x, 260, 20, 100);
        g.FillPolygon(Brushes.DarkSlateBlue, new Point[] { new(40, 230), new(180, 45), new(350, 230) });
        g.FillRectangle(Brushes.SaddleBrown, 455, 110, 25, 210);
        g.FillEllipse(Brushes.ForestGreen, 380, 30, 180, 160);
        g.FillEllipse(Brushes.Gold, 300, 45, 45, 45);
        image.Save(path, ImageFormat.Png);
    }
}
