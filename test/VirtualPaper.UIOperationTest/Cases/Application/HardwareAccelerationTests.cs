using System.Diagnostics;
using System.Management;
using System.Security.Cryptography;
using System.Windows.Automation;
using System.Windows.Media.Imaging;
using Google.Protobuf.WellKnownTypes;
using VirtualPaper.Common;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class HardwareAccelerationTests {
    public static async Task StartupPolicyAndOptionsAsync(UiTestContext context) {
        await using var session = new AppProcessSession();
        await session.StartAsync(settings => settings.UiHardwareAccelerationEnabled = true);
        var ui = await session.OpenUiAsync();
        await VerifyOptionsAsync(ui, enabled: true);
        await Task.Run(() => {
            var animation = ApplicationUi.Find(OpenGeneral(ui), "Settings.General.InterfaceAnimations");
            ApplicationUi.BringIntoView(animation);
            var toggle = (TogglePattern)animation.GetCurrentPattern(TogglePattern.Pattern);
            Check(toggle.Current.ToggleState == ToggleState.On, "Animation preference did not start enabled");
            toggle.Toggle();
        });
        await AppProcessSession.WaitAsync(async () => !(await session.Settings.GetSettingsAsync(new Empty(),
            deadline: DateTime.UtcNow.AddSeconds(1))).NavigationAnimationsEnabled, "Animation change reached Core");
        await AppProcessSession.WaitForJsonAsync(session.SettingsPath, json => json["NavigationAnimationsEnabled"]?.GetValue<bool>() == false);
        await CheckRestartNoticeAsync(ui, false);
        await SetPreferenceAsync(session, ui, false, restartRequired: true);
        await SetPreferenceAsync(session, ui, true, restartRequired: false);
        await SetPreferenceAsync(session, ui, false, restartRequired: true);
        await VerifyOptionsAsync(ui, enabled: true);
        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        await VerifyOptionsAsync(ui, enabled: false);
        await CheckRestartNoticeAsync(ui, false);
        await SetPreferenceAsync(session, ui, true, restartRequired: true);
        await VerifyOptionsAsync(ui, enabled: false);
        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        await VerifyOptionsAsync(ui, enabled: true);
        await CheckRestartNoticeAsync(ui, false);
        await Task.Run(() => {
            var animation = ApplicationUi.Find(OpenGeneral(ui), "Settings.General.InterfaceAnimations");
            Check(animation.Current.IsEnabled && ((TogglePattern)animation.GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState == ToggleState.Off,
                "Hardware mode changes reset or disabled the independent animation preference");
        });
        Check(session.UiIds.Distinct().Count() == 3, "Hardware preference did not use three distinct UI generations");
        await session.StopAsync();
        session.RecordEvidence(context);
        Check(((string[])context.Evidence["RuntimeErrors"]!).Length == 0, "Hardware policy acceptance logged runtime errors");
        context.Evidence["SavedPreferenceAndActivePolicySeparated"] = true;
        context.Evidence["OffAndOnAppliedAfterNormalRestart"] = true;
        context.Evidence["RevertingPreferenceClearsRestartNotice"] = true;
        context.Evidence["GpuOptionsDefaultsAndResetVerified"] = true;
        context.Evidence["AnimationPreferenceIndependentOfHardwareMode"] = true;
        context.Evidence["ActualInferenceTested"] = false;
    }

    private static async Task VerifyOptionsAsync(Process ui, bool enabled) {
        foreach (bool style in new[] { false, true }) {
            await Task.Run(() => {
                var root = AutomationElement.FromHandle(ui.MainWindowHandle);
                OpenTask(root, style);
                string activeId = style ? "Intelligent.StyleTransfer.UseGpuForEnhancement" : "Intelligent.SuperResolution.UseGpu";
                if (style) Check(!Visible(root, "Intelligent.StyleTransfer.UseGpu"),
                    "Style transfer must expose only the enhancement GPU option");
                if (enabled) {
                    var choice = ApplicationUi.Find(root, activeId);
                    ApplicationUi.BringIntoView(choice);
                    var toggle = (TogglePattern)choice.GetCurrentPattern(TogglePattern.Pattern);
                    Check(toggle.Current.ToggleState == ToggleState.On, "Recommended GPU choice was not selected by default");
                    toggle.Toggle();
                    Check(toggle.Current.ToggleState == ToggleState.Off, "GPU checkbox did not accept an explicit CPU choice");
                } else {
                    Check(!Visible(root, activeId), "Disabled startup policy still exposes GPU options");
                }
                CancelTask(root);
                OpenTask(root, style);
                if (enabled) Check(((TogglePattern)ApplicationUi.Find(root, activeId).GetCurrentPattern(TogglePattern.Pattern))
                    .Current.ToggleState == ToggleState.On, "A reopened task did not restore the recommended default");
                else Check(!Visible(root, activeId), "Reopening exposed an option under the disabled startup policy");
                CancelTask(root);
            });
        }
    }

    private static async Task SetPreferenceAsync(AppProcessSession session, Process ui, bool enabled, bool restartRequired) {
        int pid = ui.Id;
        await Task.Run(() => {
            var root = OpenGeneral(ui);
            var control = ApplicationUi.Find(root, "Settings.General.HardwareAcceleration");
            ApplicationUi.BringIntoView(control);
            var toggle = (TogglePattern)control.GetCurrentPattern(TogglePattern.Pattern);
            Check(toggle.Current.ToggleState != (enabled ? ToggleState.On : ToggleState.Off), "Hardware preference was already at its target");
            toggle.Toggle();
        });
        await AppProcessSession.WaitAsync(async () => (await session.Settings.GetSettingsAsync(new Empty(),
            deadline: DateTime.UtcNow.AddSeconds(1))).UiHardwareAccelerationEnabled == enabled, "Hardware preference reached Core");
        await AppProcessSession.WaitForJsonAsync(session.SettingsPath, json => json["UiHardwareAccelerationEnabled"]?.GetValue<bool>() == enabled);
        await CheckRestartNoticeAsync(ui, restartRequired);
        Check(!(await session.Settings.GetSettingsAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(1))).NavigationAnimationsEnabled,
            "Changing hardware acceleration reset the animation preference");
        Check(!ui.HasExited && ui.Id == pid, "Saving a hardware preference restarted the current UI");
    }

    private static Task CheckRestartNoticeAsync(Process ui, bool expected) => AppProcessSession.WaitAsync(() => Task.Run(() =>
        Visible(OpenGeneral(ui), "Settings.General.HardwareAccelerationRestart") == expected), "Hardware restart notice state");

    private static AutomationElement OpenGeneral(Process ui) {
        var root = AutomationElement.FromHandle(ui.MainWindowHandle);
        ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Settings"));
        ApplicationUi.Select(ApplicationUi.Find(root, "Settings.Tab.General"));
        return root;
    }

    // WebView2 can share a browser across UI generations. Scope discovery to this
    // run's isolated profile instead of assuming its parent is the current UI PID.
    internal static async Task VerifyWebViewAsync(UiTestContext context, Process ui, bool enabled) {
        string expected = Path.Combine(Constants.CommonPaths.TempWebView2Dir, "UI", enabled ? "GPU" : "Software");
        string relative = Path.GetRelativePath(TestProcessEnvironment.DataRoot!, expected);
        Check(TestProcessEnvironment.IsEnabled && !Path.IsPathRooted(relative) && relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal),
            "WebView discovery must stay inside the isolated test profile");
        string? browserArguments = null;
        await AppProcessSession.WaitAsync(() => Task.Run(() => {
            using var query = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE Name='msedgewebview2.exe'");
            query.Options.Timeout = TimeSpan.FromSeconds(5);
            using var processes = query.Get();
            var records = processes.Cast<ManagementObject>().Select(process => {
                using (process) return (string?)process["CommandLine"] ?? string.Empty;
            }).ToArray();
            browserArguments = records.FirstOrDefault(arguments =>
                arguments.Contains("--user-data-dir=", StringComparison.Ordinal)
                && arguments.Contains(expected, StringComparison.OrdinalIgnoreCase)
                && !arguments.Contains("--type=", StringComparison.Ordinal));
            return browserArguments != null;
        }), "Owned WebView2 browser uses the expected UI profile");
        if (!enabled) Check(browserArguments!.Contains("--disable-gpu", StringComparison.Ordinal),
            "Software WebView2 browser did not receive --disable-gpu");
        context.Evidence["UiWebViewProfile"] = expected;
        context.Evidence["UiWebViewUiProcessId"] = ui.Id;
        context.Evidence["UiWebViewBrowserArguments"] = browserArguments;
        context.Evidence["UiWebViewSoftwareArgumentVerified"] = !enabled;
    }

    public static async Task DeviceChoiceAndEnhancementAsync(UiTestContext context) {
        string core = Path.GetDirectoryName(Environment.GetEnvironmentVariable("VIRTUALPAPER_TEST_CORE_EXE"))!;
        foreach (string model in new[] {
            Path.Combine(core, Constants.WorkingDir.ML_SuperResolution_AI_Models, "realesrgan_x4plus_dynamic.onnx"),
            Path.Combine(core, Constants.WorkingDir.ML_StyleTransfer_AI_Models, "adain_style_transfer.onnx"),
            Path.Combine(core, Constants.WorkingDir.ML_StyleTransfer_AI_Models, "adain_style_transfer.onnx.data")
        }) Check(File.Exists(model), "Real hardware acceptance requires the installed model: " + model);
        var inputs = new[] { "cpu.png", "gpu.png", "style-content.png", "style.png" }
            .Select(name => Path.Combine(context.Root, name)).ToArray();
        foreach (string path in inputs) WallpaperLibraryTests.WriteImage(path, 96, 64);
        var hashes = inputs.ToDictionary(path => path, path => SHA256.HashData(File.ReadAllBytes(path)));
        await using var session = new AppProcessSession();
        await session.StartAsync(settings => settings.UiHardwareAccelerationEnabled = true);
        var ui = await session.OpenUiAsync();
        var root = await Task.Run(() => AutomationElement.FromHandle(ui.MainWindowHandle));
        var observations = new List<object>();
        foreach (var task in new[] { (Input: inputs[0], Style: false, UseGpu: false),
            (Input: inputs[1], Style: false, UseGpu: true), (Input: inputs[2], Style: true, UseGpu: true) }) {
            await Task.Run(() => {
                OpenTask(root, task.Style);
                string prefix = task.Style ? "Intelligent.StyleTransfer." : "Intelligent.SuperResolution.";
                var choice = ApplicationUi.Find(root, prefix + (task.Style ? "UseGpuForEnhancement" : "UseGpu"));
                ApplicationUi.BringIntoView(choice);
                var toggle = (TogglePattern)choice.GetCurrentPattern(TogglePattern.Pattern);
                Check(toggle.Current.ToggleState == ToggleState.On, "GPU default was not restored before submitting the next task");
                if (!task.UseGpu) toggle.Toggle();
                ApplicationUi.Invoke(ApplicationUi.Find(root, prefix + "Source"));
                NativeFileDialog.OpenFile(ui, ui.MainWindowHandle, task.Input);
                if (task.Style) {
                    ApplicationUi.Invoke(ApplicationUi.Find(root, prefix + "Style"));
                    NativeFileDialog.OpenFile(ui, ui.MainWindowHandle, inputs[3]);
                }
                ApplicationUi.Invoke(ApplicationUi.Find(root, "Intelligent.Task.Confirm", visible: true));
            });
            await AppProcessSession.WaitAsync(() => Task.Run(() => !ApplicationUi.Find(root,
                "Intelligent.Task.Confirm", visible: true, enabled: false).Current.IsEnabled), "Task input reset after submission");
            await Task.Run(() => CancelTask(root));
            await AppProcessSession.WaitAsync(() => Task.Run(() => {
                string status = ApplicationUi.Find(Row(root, task.Input, task.Style), "Intelligent.Task.Status").Current.Name;
                Check(status != LanguageUtil.GetI18n(Constants.I18n.Text_Task_Status_Failed), "Real device-choice inference failed");
                return status == LanguageUtil.GetI18n(Constants.I18n.Text_Task_Status_Completed);
            }), "Real task completes using its selected device", TimeSpan.FromMinutes(6));
            string backend = await Task.Run(() => ApplicationUi.Find(Row(root, task.Input, task.Style), "Intelligent.Task.Backend").Current.Name);
            var allowed = !task.UseGpu ? new[] { Constants.I18n.Intelligent_Backend_Cpu } : task.Style
                ? new[] { Constants.I18n.Intelligent_Backend_Mixed, Constants.I18n.Intelligent_Backend_CpuFallback, Constants.I18n.Intelligent_Backend_MixedCpuFallback }
                : new[] { Constants.I18n.Intelligent_Backend_Gpu, Constants.I18n.Intelligent_Backend_CpuFallback, Constants.I18n.Intelligent_Backend_MixedCpuFallback };
            Check(allowed.Select(LanguageUtil.GetI18n).Contains(backend), "Task backend does not match its GPU preference: " + backend);
            string output = Path.Combine(context.Root, "result-" + Path.GetFileName(task.Input));
            await Task.Run(() => {
                ApplicationUi.Invoke(ApplicationUi.Find(Row(root, task.Input, task.Style), "Intelligent.Task.Save"));
                NativeFileDialog.SaveFile(ui, ui.MainWindowHandle, output);
            });
            await AppProcessSession.WaitAsync(() => Task.Run(() => {
                try {
                    if (!File.Exists(output) || new FileInfo(output).Length < 32) return false;
                    using var stream = File.OpenRead(output);
                    var image = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    Check(image.PixelWidth == (task.Style ? 96 : 192) && image.PixelHeight == (task.Style ? 64 : 128),
                        "Real device-choice export has incorrect dimensions");
                    return ApplicationUi.Find(Row(root, task.Input, task.Style), "Intelligent.Task.Preservation").Current.Name
                        == LanguageUtil.GetI18n(Constants.I18n.Intelligent_Result_Exported);
                }
                catch (IOException) { return false; }
                catch (FileFormatException) { return false; }
            }), "Actual result exported and preservation badge updated");
            observations.Add(new { task.Style, task.UseGpu, Backend = backend,
                GpuCompletedWithoutFallback = backend == LanguageUtil.GetI18n(Constants.I18n.Intelligent_Backend_Gpu)
                    || backend == LanguageUtil.GetI18n(Constants.I18n.Intelligent_Backend_Mixed),
                CpuFallbackObserved = backend == LanguageUtil.GetI18n(Constants.I18n.Intelligent_Backend_CpuFallback)
                    || backend == LanguageUtil.GetI18n(Constants.I18n.Intelligent_Backend_MixedCpuFallback) });
            context.Evidence["DeviceChoiceResults"] = observations.ToArray();
            await Task.Run(() => ApplicationUi.Invoke(ApplicationUi.Find(Row(root, task.Input, task.Style), "Intelligent.Task.Remove")));
            await AppProcessSession.WaitAsync(() => Task.Run(() => !root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty,
                    task.Style ? "Intelligent.StyleTransfer.Task" : "Intelligent.SuperResolution.Task"))
                .Cast<AutomationElement>().Any(item => item.Current.Name == task.Input)), "Exported device-choice task removed");
        }
        foreach (var input in hashes) Check(Enumerable.SequenceEqual(input.Value, SHA256.HashData(await File.ReadAllBytesAsync(input.Key))),
            "Real inference modified a source image");
        await session.StopAsync();
        session.RecordEvidence(context);
        Check(((string[])context.Evidence["RuntimeErrors"]!).Length == 0, "Device-choice acceptance logged runtime errors");
        context.Evidence["PhysicalGpuFailureInjected"] = false;
    }

    private static AutomationElement Row(AutomationElement root, string source, bool style) {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(10)) {
            try {
                var row = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty,
                    style ? "Intelligent.StyleTransfer.Task" : "Intelligent.SuperResolution.Task"))
                    .Cast<AutomationElement>().SingleOrDefault(item => item.Current.Name == source);
                if (row != null) return row;
            }
            catch (ElementNotAvailableException) { }
            Thread.Sleep(50);
        }
        throw new TimeoutException("Device-choice task row not found: " + source);
    }

    private static void OpenTask(AutomationElement root, bool style) {
        ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Intelligent"));
        ApplicationUi.Select(ApplicationUi.Find(root, style ? "Intelligent.Tab.StyleTransfer" : "Intelligent.Tab.SuperResolution"));
        ApplicationUi.Invoke(ApplicationUi.Find(root, "Intelligent.AddTask"));
        ApplicationUi.Find(root, "Intelligent.Task.Cancel", visible: true);
    }

    private static void CancelTask(AutomationElement root) {
        ApplicationUi.Invoke(ApplicationUi.Find(root, "Intelligent.Task.Cancel", visible: true));
        ApplicationUi.WaitUntilGone(root, "Intelligent.Task.Cancel");
    }

    // Scrolled-out controls are still laid out; they must not masquerade as a hidden option.
    private static bool Visible(AutomationElement root, string id) => root.FindAll(TreeScope.Descendants,
        new PropertyCondition(AutomationElement.AutomationIdProperty, id)).Cast<AutomationElement>()
        .Any(element => element.Current.BoundingRectangle is { Width: > 0, Height: > 0 });
}
