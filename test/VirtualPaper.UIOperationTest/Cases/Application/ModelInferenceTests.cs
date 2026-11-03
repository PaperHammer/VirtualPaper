using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Automation;
using System.Windows.Media.Imaging;
using Google.Protobuf.WellKnownTypes;
using VirtualPaper.Common;
using VirtualPaper.Models.Cores;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class ModelInferenceTests {
    public static async Task RunAsync(UiTestContext context) {
        string coreFolder = Path.GetDirectoryName(Environment.GetEnvironmentVariable("VIRTUALPAPER_TEST_CORE_EXE"))!;
        string model = Path.Combine(coreFolder, Constants.WorkingDir.ML_SuperResolution_AI_Models, "realesrgan_x4plus_dynamic.onnx");
        Check(File.Exists(model), "Explicit integration test requires the installed Real-ESRGAN model: " + model);
        context.Evidence["ModelBytes"] = new FileInfo(model).Length;
        string[] inputs = Enumerable.Range(1, 3).Select(index => Path.Combine(context.Root, $"inference-{index}.png")).ToArray();
        foreach (string input in inputs) WallpaperLibraryTests.WriteImage(input, 96, 64);
        string output = Path.Combine(context.Root, "enhanced.png");
        byte[] originalHash = SHA256.HashData(await File.ReadAllBytesAsync(inputs[0]));
        await using var session = new AppProcessSession();
        await session.StartAsync();
        context.Evidence["CoreProcessId"] = session.CoreId;
        var ui = await session.OpenUiAsync();
        nint owner = ui.MainWindowHandle;
        var root = await Task.Run(() => {
            var window = AutomationElement.FromHandle(owner);
            ApplicationUi.Select(ApplicationUi.Find(window, "App.Navigation.Intelligent"));
            ApplicationUi.Select(ApplicationUi.Find(window, "Intelligent.Tab.SuperResolution"));
            ApplicationUi.Invoke(ApplicationUi.Find(window, "Intelligent.AddTask"));
            return window;
        });
        var timer = Stopwatch.StartNew();
        foreach (string input in inputs) {
            await Task.Run(() => {
                ApplicationUi.Invoke(ApplicationUi.Find(root, "Intelligent.SuperResolution.Source", visible: true));
                NativeFileDialog.OpenFile(ui, owner, input);
                ApplicationUi.Invoke(ApplicationUi.Find(root, "Intelligent.Task.Confirm", visible: true));
            });
            // A successful submission clears the form; wait before selecting the next input.
            await AppProcessSession.WaitAsync(() => Task.Run(() => !ApplicationUi.Find(root,
                "Intelligent.Task.Confirm", visible: true, enabled: false).Current.IsEnabled), "Task accepted and input reset");
        }
        await Task.Run(() => {
            ApplicationUi.Invoke(ApplicationUi.Find(root, "Intelligent.Task.Cancel", visible: true));
            ApplicationUi.WaitUntilGone(root, "Intelligent.Task.Cancel");
            Check(Status(root, inputs[2]) == LanguageUtil.GetI18n(Constants.I18n.Text_Task_Status_Queue), "Third task was not queued behind the two running tasks");
            Check(Status(root, inputs[1]) == LanguageUtil.GetI18n(Constants.I18n.Text_Task_Status_Processing), "Second task was not running before cancellation");
            ApplicationUi.Invoke(ApplicationUi.Find(Row(root, inputs[2]), "Intelligent.Task.Remove"));
            ApplicationUi.Invoke(ApplicationUi.Find(Row(root, inputs[1]), "Intelligent.Task.Remove"));
        });
        await AppProcessSession.WaitAsync(() => Task.Run(() => FindRows(root).Count == 1), "Queued and running tasks removed");
        context.Evidence["QueuedAndRunningCancellationVerified"] = true;
        await AppProcessSession.WaitAsync(() => Task.Run(() => {
            string state = Status(root, inputs[0]);
            Check(state != LanguageUtil.GetI18n(Constants.I18n.Text_Task_Status_Failed), "Real model inference failed");
            return state == LanguageUtil.GetI18n(Constants.I18n.Text_Task_Status_Completed);
        }), "Real model result completed", TimeSpan.FromMinutes(6));
        context.Evidence["InferenceMilliseconds"] = timer.ElapsedMilliseconds;
        await Task.Run(() => {
            ApplicationUi.Invoke(ApplicationUi.Find(Row(root, inputs[0]), "Intelligent.Task.Save"));
            NativeFileDialog.SaveFile(ui, owner, output);
        });
        await AppProcessSession.WaitAsync(() => Task.Run(() => {
            try {
                if (!File.Exists(output) || new FileInfo(output).Length < 32) return false;
                using var file = File.OpenRead(output);
                var image = BitmapFrame.Create(file, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                Check(image.PixelWidth == 192 && image.PixelHeight == 128, "Actual model result has incorrect dimensions");
                return true;
            }
            catch (IOException) { return false; }
            catch (FileFormatException) { return false; }
        }), "Real result exported as PNG");
        await Task.Run(() => ApplicationUi.Invoke(ApplicationUi.Find(Row(root, inputs[0]), "Intelligent.Task.Import")));
        string library = (await session.Settings.GetSettingsAsync(new Empty())).WallpaperDir;
        WpBasicData? imported = null;
        await AppProcessSession.WaitAsync(async () => {
            var metadata = Directory.GetFiles(library, Constants.Field.WpBasicDataFileName, SearchOption.AllDirectories);
            if (metadata.Length != 1) return false;
            try {
                imported = JsonSerializer.Deserialize(await AppProcessSession.ReadTextSharedAsync(metadata[0]), WpBasicDataContext.Default.WpBasicData);
                return imported != null && File.Exists(imported.FilePath) && File.Exists(imported.ThumbnailPath);
            }
            catch (IOException) { return false; }
            catch (JsonException) { return false; }
        }, "Real result imported into library");
        Check(SHA256.HashData(await File.ReadAllBytesAsync(output)).SequenceEqual(SHA256.HashData(await File.ReadAllBytesAsync(imported!.FilePath))), "Imported image differs from the actual exported result");
        await Task.Run(() => ApplicationUi.Invoke(ApplicationUi.Find(Row(root, inputs[0]), "Intelligent.Task.Remove")));
        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        await Task.Run(() => {
            var window = AutomationElement.FromHandle(ui.MainWindowHandle);
            ApplicationUi.Select(ApplicationUi.Find(window, "App.Navigation.Library"));
            WallpaperLibraryTests.FindItem(window, imported.WallpaperUid);
        });
        Check(originalHash.SequenceEqual(SHA256.HashData(await File.ReadAllBytesAsync(inputs[0]))), "Inference modified the source file");
        await session.StopAsync();
        session.RecordEvidence(context);
        Check(((string[])context.Evidence["RuntimeErrors"]!).Length == 0, "Model integration logged a runtime error");
        context.Evidence["RealInferenceSaveImportAndReopenVerified"] = true;
    }

    private static List<AutomationElement> FindRows(AutomationElement root) => root.FindAll(TreeScope.Descendants,
        new PropertyCondition(AutomationElement.AutomationIdProperty, "Intelligent.SuperResolution.Task")).Cast<AutomationElement>().ToList();
    private static AutomationElement Row(AutomationElement root, string path) => FindRows(root).Single(item => item.Current.Name == path);
    private static string Status(AutomationElement root, string path) => ApplicationUi.Find(Row(root, path), "Intelligent.Task.Status").Current.Name;
}
