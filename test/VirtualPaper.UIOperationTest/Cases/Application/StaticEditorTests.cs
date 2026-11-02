using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VirtualPaper.UIOperationTest.Infrastructure;
using Workloads.Creation.StaticImg.Models.SerializableData;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class StaticEditorTests {
    public static async Task RotateCancelSaveReopenAndExportAsync(UiTestContext context) {
        string source = Path.Combine(context.Root, "source.png");
        string design = Path.Combine(context.Root, "rotated.vpd");
        string output = Path.Combine(context.Root, "exported.png");
        WallpaperLibraryTests.WriteImage(source);
        byte[] sourceHash = SHA256.HashData(await File.ReadAllBytesAsync(source));
        await using var session = new AppProcessSession();
        await session.StartAsync();
        context.Evidence["CoreProcessId"] = session.CoreId;
        var ui = await session.OpenUiAsync();
        nint ownerWindow = ui.MainWindowHandle;
        var root = await Task.Run(() => OpenImage(ui, ownerWindow, source));
        await WaitForDimensionsAsync(root, 1920, 1080);
        await Task.Run(() => ApplicationUi.Invoke(ApplicationUi.Find(root, "StaticEditor.RotateLeft")));
        await WaitForDimensionsAsync(root, 1080, 1920);
        context.Evidence["CanvasRotated"] = true;
        await Task.Run(() => {
            Check(ui.CloseMainWindow(), "Editor did not accept the unsaved-close request");
            ApplicationUi.Find(root, "PrimaryButton", visible: true);
            ApplicationUi.Invoke(ApplicationUi.Find(root, "CloseButton", visible: true));
            ApplicationUi.WaitUntilGone(root, "PrimaryButton");
            Check(!ui.HasExited, "Cancelling the unsaved dialog closed the editor UI");
            InvokeFileAction(root, "Project.File.Save");
            NativeFileDialog.Cancel(ui, ownerWindow);
        });
        Check(!File.Exists(design), "Cancelling Save created a design file");
        context.Evidence["UnsavedCloseAndSaveCancelled"] = true;
        await Task.Run(() => {
            InvokeFileAction(root, "Project.File.Save");
            NativeFileDialog.SaveFile(ui, ownerWindow, design);
        });
        await AppProcessSession.WaitAsync(() => Task.FromResult(IsCompleteFile(design, Marshal.SizeOf<FileHeader>())), "Design file saved");
        var header = await StaticImgDesignFileUtil.GetFileHeaderAsync(design);
        Check(header.HasValue && header.Value.CanvasWidth == 1080 && header.Value.CanvasHeight == 1920
            && header.Value.LayerCount > 0, "Saved design header lost the rotated canvas or layers");
        context.Evidence["DesignSaved"] = true;
        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        ownerWindow = ui.MainWindowHandle;
        root = await Task.Run(() => OpenImage(ui, ownerWindow, design));
        await WaitForDimensionsAsync(root, 1080, 1920);
        await Task.Run(() => {
            ApplicationUi.Expand(ApplicationUi.Find(root, "Project.File"));
            ApplicationUi.Expand(ApplicationUi.Find(root, "Project.File.Export", visible: true));
            ApplicationUi.Invoke(ApplicationUi.Find(root, "Project.File.ExportPng", visible: true));
            NativeFileDialog.SaveFile(ui, ownerWindow, output);
        });
        await AppProcessSession.WaitAsync(() => Task.Run(() => {
            if (!IsCompleteFile(output, 32)) return false;
            try {
                using var stream = File.OpenRead(output);
                var bitmap = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                Check(bitmap.PixelWidth == 1080 && bitmap.PixelHeight == 1920, "Exported PNG dimensions differ from the reopened canvas");
                var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
                byte[] pixels = new byte[1080 * 1920 * 4];
                converted.CopyPixels(pixels, 1080 * 4, 0);
                var blue = Enumerable.Range(0, pixels.Length / 4).Select(index => pixels[index * 4]).ToArray();
                Check(blue.Max() - blue.Min() > 100 && pixels.Where((_, index) => index % 4 == 3).Any(alpha => alpha > 0),
                    "Exported PNG lost the source image content");
                return true;
            }
            catch (IOException) { return false; }
            catch (FileFormatException) { return false; }
        }), "Reopened design exported a complete PNG");
        Check(sourceHash.SequenceEqual(SHA256.HashData(await File.ReadAllBytesAsync(source))), "Editor modified the source image");
        await session.StopAsync();
        session.RecordEvidence(context);
        Check(((string[])context.Evidence["RuntimeErrors"]!).Length == 0, "Static editor logged a runtime error; inspect report evidence");
        context.Evidence["SavedCanvasWidth"] = 1080;
        context.Evidence["SavedCanvasHeight"] = 1920;
        context.Evidence["SavedLayerCount"] = header!.Value.LayerCount;
        context.Evidence["RotateCancelSaveReopenAndExportVerified"] = true;
        context.Evidence["BrushAndLayerRenameTested"] = false;
    }

    private static AutomationElement OpenImage(System.Diagnostics.Process ui, nint ownerWindow, string path) {
        var root = AutomationElement.FromHandle(ownerWindow);
        ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Projects"));
        ApplicationUi.Invoke(ApplicationUi.Find(root, "Projects.Open"));
        NativeFileDialog.OpenFile(ui, ownerWindow, path);
        ApplicationUi.Select(ApplicationUi.Find(root, "StaticEditor.Tool.CanvasSet"));
        ApplicationUi.Expand(ApplicationUi.Find(root, "StaticEditor.Canvas"));
        return root;
    }

    private static Task WaitForDimensionsAsync(AutomationElement root, int width, int height) =>
        AppProcessSession.WaitAsync(() => Task.Run(() => ReadNumber(root, "StaticEditor.Width") == width
            && ReadNumber(root, "StaticEditor.Height") == height), $"Editor canvas {width} by {height}");

    private static double ReadNumber(AutomationElement root, string id) => double.Parse(
        ((ValuePattern)ApplicationUi.Find(root, id, visible: true).GetCurrentPattern(ValuePattern.Pattern)).Current.Value,
        CultureInfo.InvariantCulture);

    private static void InvokeFileAction(AutomationElement root, string id) {
        ApplicationUi.Expand(ApplicationUi.Find(root, "Project.File"));
        ApplicationUi.Invoke(ApplicationUi.Find(root, id, visible: true));
    }

    private static bool IsCompleteFile(string path, int minimumLength) {
        try {
            if (!File.Exists(path) || new FileInfo(path).Length <= minimumLength) return false;
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return file.Length > minimumLength;
        }
        catch (IOException) { return false; }
    }
}
