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
        // Both the tray/Core path and WM_CLOSE must protect tasks even after
        // navigating away from the intelligent page.
        await Task.Run(() => {
            ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Library"));
            var menu = TrayUi.Open(session.CoreId);
            ApplicationUi.Invoke(ApplicationUi.Find(menu.Element, "Tray.Exit", visible: true));
            ApplicationUi.Find(root, "Intelligent.Close.Dialog", visible: true);
            ApplicationUi.Invoke(ApplicationUi.DialogButton(root, "PrimaryButton"));
            ApplicationUi.WaitUntilGone(root, "Intelligent.Close.Dialog");
        });
        await session.WaitForShutdownCancellationAsync(1);
        Check(!ui.HasExited, "Returning from the intelligent exit dialog terminated UI");
        await Task.Run(() => {
            ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Intelligent"));
            ApplicationUi.Select(ApplicationUi.Find(root, "Intelligent.Tab.SuperResolution"));
        });
        await AppProcessSession.WaitAsync(() => Task.Run(() => FindRows(root).Count == 1), "Queued and running tasks removed");
        context.Evidence["QueuedAndRunningCancellationVerified"] = true;
        await AppProcessSession.WaitAsync(() => Task.Run(() => {
            string state = Status(root, inputs[0]);
            Check(state != LanguageUtil.GetI18n(Constants.I18n.Text_Task_Status_Failed), "Real model inference failed");
            return state == LanguageUtil.GetI18n(Constants.I18n.Text_Task_Status_Completed);
        }), "Real model result completed", TimeSpan.FromMinutes(6));
        context.Evidence["InferenceMilliseconds"] = timer.ElapsedMilliseconds;
        await WaitForPreservationAsync(root, inputs[0], Constants.I18n.Intelligent_Result_Temporary);
        // Ticking the suppression box and returning must neither save the
        // preference nor discard the temporary result. The next close asks again.
        await Task.Run(() => {
            ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Library"));
            Check(ui.CloseMainWindow(), "UI rejected the close request");
            ApplicationUi.Find(root, "Intelligent.Close.Dialog", visible: true);
            var checkbox = ApplicationUi.Find(root, "Intelligent.Close.DontRemind", visible: true);
            ((TogglePattern)checkbox.GetCurrentPattern(TogglePattern.Pattern)).Toggle();
            ApplicationUi.Invoke(ApplicationUi.DialogButton(root, "PrimaryButton"));
            ApplicationUi.WaitUntilGone(root, "Intelligent.Close.Dialog");
            Check(ui.CloseMainWindow(), "UI rejected the repeated close request");
            ApplicationUi.Find(root, "Intelligent.Close.Dialog", visible: true);
            ApplicationUi.Invoke(ApplicationUi.DialogButton(root, "PrimaryButton"));
            ApplicationUi.WaitUntilGone(root, "Intelligent.Close.Dialog");
            ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Intelligent"));
            ApplicationUi.Select(ApplicationUi.Find(root, "Intelligent.Tab.SuperResolution"));
            ApplicationUi.Invoke(ApplicationUi.Find(Row(root, inputs[0]), "Intelligent.Task.Remove"));
            ApplicationUi.Find(root, "Intelligent.Remove.Dialog", visible: true);
            ApplicationUi.Invoke(ApplicationUi.DialogButton(root, "PrimaryButton"));
            ApplicationUi.WaitUntilGone(root, "Intelligent.Remove.Dialog");
            ApplicationUi.Invoke(ApplicationUi.Find(Row(root, inputs[0]), "Intelligent.Task.Save"));
            NativeFileDialog.Cancel(ui, owner);
        });
        Check((await session.Settings.GetSettingsAsync(new Empty())).RemindUnpreservedIntelligentResults,
            "Cancelled exit changed the reminder preference");
        await WaitForPreservationAsync(root, inputs[0], Constants.I18n.Intelligent_Result_Temporary);
        context.Evidence["TemporaryResultCloseAndDeleteCancellationVerified"] = true;
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
        await WaitForPreservationAsync(root, inputs[0], Constants.I18n.Intelligent_Result_Exported);
        await Task.Run(() => ApplicationUi.Invoke(ApplicationUi.Find(Row(root, inputs[0]), "Intelligent.Task.Import")));
        string library = (await session.Settings.GetSettingsAsync(new Empty())).WallpaperDir;
        WpBasicData? imported = null;
        await AppProcessSession.WaitAsync(async () => {
            var metadata = Directory.GetFiles(library, Constants.Field.WpBasicDataFileName, SearchOption.AllDirectories);
            if (metadata.Length != 1) return false;
            try {
                imported = JsonSerializer.Deserialize(await AppProcessSession.ReadTextSharedAsync(metadata[0]), WpBasicDataContext.Default.WpBasicData);
                imported?.ResolvePaths(metadata[0]);
                return imported != null && File.Exists(imported.FilePath) && File.Exists(imported.ThumbnailPath);
            }
            catch (IOException) { return false; }
            catch (JsonException) { return false; }
        }, "Real result imported into library");
        Check(SHA256.HashData(await File.ReadAllBytesAsync(output)).SequenceEqual(SHA256.HashData(await File.ReadAllBytesAsync(imported!.FilePath))), "Imported image differs from the actual exported result");
        await WaitForPreservationAsync(root, inputs[0], Constants.I18n.Intelligent_Result_ExportedAndImported);
        await Task.Run(() => ApplicationUi.Invoke(ApplicationUi.Find(Row(root, inputs[0]), "Intelligent.Task.Remove")));
        // A second real result exercises confirmed suppression and persisted settings.
        await Task.Run(() => {
            ApplicationUi.Invoke(ApplicationUi.Find(root, "Intelligent.AddTask"));
            ApplicationUi.Invoke(ApplicationUi.Find(root, "Intelligent.SuperResolution.Source", visible: true));
            NativeFileDialog.OpenFile(ui, owner, inputs[0]);
            ApplicationUi.Invoke(ApplicationUi.Find(root, "Intelligent.Task.Confirm", visible: true));
        });
        await AppProcessSession.WaitAsync(() => Task.Run(() => !ApplicationUi.Find(root,
            "Intelligent.Task.Confirm", visible: true, enabled: false).Current.IsEnabled), "Second task accepted");
        await Task.Run(() => {
            ApplicationUi.Invoke(ApplicationUi.Find(root, "Intelligent.Task.Cancel", visible: true));
            ApplicationUi.WaitUntilGone(root, "Intelligent.Task.Cancel");
        });
        await AppProcessSession.WaitAsync(() => Task.Run(() => Status(root, inputs[0]) ==
            LanguageUtil.GetI18n(Constants.I18n.Text_Task_Status_Completed)), "Second real inference completed", TimeSpan.FromMinutes(6));
        Task close = session.CloseUiAsync();
        await Task.Run(() => {
            ApplicationUi.Find(root, "Intelligent.Close.Dialog", visible: true);
            var checkbox = ApplicationUi.Find(root, "Intelligent.Close.DontRemind", visible: true);
            ((TogglePattern)checkbox.GetCurrentPattern(TogglePattern.Pattern)).Toggle();
            ApplicationUi.Invoke(ApplicationUi.DialogButton(root, "SecondaryButton"));
        });
        await close;
        Check(!(await session.Settings.GetSettingsAsync(new Empty())).RemindUnpreservedIntelligentResults,
            "Confirmed suppression was not persisted through Core");
        ui = await session.OpenUiAsync();
        await Task.Run(() => {
            var window = AutomationElement.FromHandle(ui.MainWindowHandle);
            ApplicationUi.Select(ApplicationUi.Find(window, "App.Navigation.Library"));
            WallpaperLibraryTests.FindItem(window, imported.WallpaperUid);
            ApplicationUi.Select(ApplicationUi.Find(window, "App.Navigation.Settings"));
            var toggle = ApplicationUi.Find(window, "Settings.General.IntelligentResultReminder");
            ApplicationUi.BringIntoView(toggle);
            Check(((TogglePattern)toggle.GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState == ToggleState.Off,
                "Reopened UI did not reflect the saved reminder preference");
            ((TogglePattern)toggle.GetCurrentPattern(TogglePattern.Pattern)).Toggle();
        });
        await AppProcessSession.WaitAsync(async () => (await session.Settings.GetSettingsAsync(new Empty())).RemindUnpreservedIntelligentResults,
            "Reminder re-enabled from settings");
        context.Evidence["ActiveTaskTrayExitCancellationVerified"] = true;
        context.Evidence["PreservationBadgesAndReminderPersistenceVerified"] = true;
        Check(originalHash.SequenceEqual(SHA256.HashData(await File.ReadAllBytesAsync(inputs[0]))), "Inference modified the source file");
        await session.StopAsync();
        session.RecordEvidence(context);
        Check(((string[])context.Evidence["RuntimeErrors"]!).Length == 0, "Model integration logged a runtime error");
        context.Evidence["RealInferenceSaveImportAndReopenVerified"] = true;
    }

    private static Task WaitForPreservationAsync(AutomationElement root, string path, string key) =>
        AppProcessSession.WaitAsync(() => Task.Run(() => ApplicationUi.Find(Row(root, path),
            "Intelligent.Task.Preservation", visible: true).Current.Name == LanguageUtil.GetI18n(key)), "Result preservation " + key);

    private static List<AutomationElement> FindRows(AutomationElement root) => root.FindAll(TreeScope.Descendants,
        new PropertyCondition(AutomationElement.AutomationIdProperty, "Intelligent.SuperResolution.Task")).Cast<AutomationElement>().ToList();
    private static AutomationElement Row(AutomationElement root, string path) {
        // Navigation can finish after Select returns. Wait for the specific row,
        // while still rejecting duplicate tasks with the same source path.
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(10)) {
            try {
                var row = FindRows(root).SingleOrDefault(item => item.Current.Name == path);
                if (row != null) return row;
            }
            catch (ElementNotAvailableException) { }
            Thread.Sleep(100);
        }
        throw new TimeoutException($"Intelligent task row not found: {path}");
    }
    private static string Status(AutomationElement root, string path) => ApplicationUi.Find(Row(root, path), "Intelligent.Task.Status").Current.Name;
}
