using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIOperationTest.Cases;
using VirtualPaper.UIOperationTest.Infrastructure;

namespace VirtualPaper.UIOperationTest;

public partial class App : Application {
    private Window? _window;

    public App() => InitializeComponent();

    protected override async void OnLaunched(LaunchActivatedEventArgs args) {
        var commandLine = Environment.GetCommandLineArgs();
        if ((commandLine.Length != 3 && commandLine.Length != 5) || commandLine[1] != "--report"
            || (commandLine.Length == 5 && commandLine[3] != "--module"
                && (commandLine[3] != "--suite" || commandLine[4] != "Extended"))) {
            Environment.ExitCode = 2;
            Exit();
            return;
        }

        var cases = new[] {
            new UiTestCase("Localization.LiveText.RefreshAndPreserveState", LocalizationTests.RefreshAndPreserveStateAsync),
            new UiTestCase("Localization.LongText.IntelligentLayouts", LocalizationLayoutTests.RunAsync),
            new UiTestCase("GeneralEffect.RImage.EditRestoreSaveAndReopen",
                context => GeneralEffectTests.EditRestoreSaveAndReopenAsync(context, RuntimeType.RImage)),
            new UiTestCase("GeneralEffect.RImage3D.EditRestoreSaveAndReopen",
                context => GeneralEffectTests.EditRestoreSaveAndReopenAsync(context, RuntimeType.RImage3D)),
            new UiTestCase("Application.NavigationSettings.SaveAndReopen", NavigationSettingsTests.SaveAndReopenAsync),
            new UiTestCase("Application.PerformanceSettings.SaveAndReopen", PerformanceSettingsTests.SaveAndReopenAsync),
            new UiTestCase("Application.WallpaperLibrary.ImportApplyCloseDelete", WallpaperLibraryTests.ImportApplyCloseDeleteAsync),
            new UiTestCase("Application.WallpaperLibrary.ImportMultipleFormatsAndReopen", BuiltInWallpaperTests.ImportMultipleAsync),
            new UiTestCase("Application.WallpaperLibrary.BuiltInsApplyRenderAndClose", BuiltInWallpaperTests.ApplyBuiltInsAsync),
            new UiTestCase("Application.WallpaperLibrary.OrganizeAndReopen", LibraryOrganizationTests.RunAsync),
            new UiTestCase("Application.EffectPresetManager.CreateCopyDeleteAndReopen", EffectPresetManagerTests.RunAsync),
            new UiTestCase("Application.ScreenSaver.SettingsAndWhitelist", ScreenSaverTests.SettingsAndWhitelistAsync),
            new UiTestCase("Application.ScreenFit.PreviewCancelApplyAndReopen", ScreenFitTests.PreviewCancelApplyAndReopenAsync),
            new UiTestCase("Application.WallpaperEffects.EditRestoreSaveAndReopen", WallpaperEffectsTests.EditRestoreSaveAndReopenAsync),
            new UiTestCase("Application.GeneralSettings.SaveLanguageThemeAndReopen", GeneralSettingsTests.SaveLanguageThemeAndReopenAsync),
            new UiTestCase("Application.ThemeSync.MainWindowAndTray", ThemeSyncTests.MainWindowAndTrayAsync),
            new UiTestCase("Application.SystemSettings.ExportLogsAndCancel", SystemSettingsTests.ExportLogsAndCancelAsync),
            new UiTestCase("Application.StaticEditor.RotateCancelSaveReopenAndExport", StaticEditorTests.RotateCancelSaveReopenAndExportAsync),
            new UiTestCase("Application.WebEditor.CreateFilesSaveReopenAndExport", WebEditorTests.CreateFilesSaveReopenAndExportAsync),
            new UiTestCase("Application.EditorExit.ImageCancelSaveAndExit", EditorExitTests.ImageAsync),
            new UiTestCase("Application.EditorExit.WebCancelSaveAndDiscard", EditorExitTests.WebAsync),
            new UiTestCase("Application.Intelligent.ConfigureCancelAndReopen", IntelligentTests.ConfigureCancelAndReopenAsync),
            new UiTestCase("Application.ModelInference.SuperResolutionQueueCancelSaveAndImport", ModelInferenceTests.RunAsync, true),
            new UiTestCase("Application.ScreenSaverLifecycle.NavigateNotifyCloseAndReopen", ScreenSaverLifecycleTests.RunAsync, true),
            new UiTestCase("Application.Rendering.DepthInferenceDrawPauseAndResume", RenderingTests.RunAsync, true),
            new UiTestCase("Application.ScreenSaverIdle.RealIdleInputWakeAndRepeat", ScreenSaverIdleTests.RunAsync, true,
                RequiresIdleEnvironment: true)
        };
        // Installed-package lifecycle stages are invoked explicitly around a real
        // reinstall; they must not overwrite the regular suite's isolated profile.
        if (PackageActivation.ExpectedFullName != null && commandLine.Length == 5 && commandLine[3] == "--module")
            cases = [.. cases,
                new UiTestCase("Application.MsixProfile.Prepare", MsixProfileTests.PrepareAsync),
                new UiTestCase("Application.MsixProfile.Verify", MsixProfileTests.VerifyAsync)];
        if (commandLine.Length == 5 && commandLine[3] == "--module")
            cases = cases.Where(test => test.Name == "Application." + commandLine[4] ||
                test.Name.StartsWith("Application." + commandLine[4] + ".", StringComparison.Ordinal)).ToArray();
        else if (commandLine.Length == 5)
            cases = cases.Where(test => !test.RequiresIdleEnvironment).ToArray();
        else
            cases = cases.Where(test => !test.RequiresIntegrationEnvironment).ToArray();
        var results = new List<UiTestResult>();
        try {
            CrossThreadInvoker.Initialize(new UiSynchronizationContext());
            UiAnimation.Initialize(true);
            await LanguageUtil.InitializeLocalizerForUnpackaged("en-US");
            _window = new Window { Title = "VirtualPaper UI operation tests" };
            // Keep the same UI thread and window alive; each case owns its page and files.
            foreach (var testCase in cases) {
                var timer = Stopwatch.StartNew();
                string? error = null;
                Dictionary<string, object?> evidence = [];
                try {
                    using var context = new UiTestContext(_window);
                    evidence = context.Evidence;
                    await testCase.Run(context);
                }
                catch (Exception ex) {
                    error = ex.ToString();
                    FailureArtifacts.Save(testCase.Name, evidence);
                }
                results.Add(new(testCase.Name, error == null,
                    timer.ElapsedMilliseconds, error, evidence));
            }
        }
        catch (Exception ex) {
            results.Add(new("Host.Initialization", false, 0, ex.ToString(), []));
        }
        finally {
            bool passed = cases.Length > 0 && results.Count == cases.Length && results.All(result => result.Passed);
            Environment.ExitCode = passed ? 0 : 1;
            try {
                var reportPath = Path.GetFullPath(commandLine[2]);
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
                File.WriteAllText(reportPath, JsonSerializer.Serialize(new { Passed = passed, ExpectedCount = cases.Length, Results = results },
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            finally {
                _window?.Close();
                UiAnimation.Shutdown();
                Exit();
            }
        }
    }

    private sealed record UiTestCase(string Name, Func<UiTestContext, Task> Run,
        bool RequiresIntegrationEnvironment = false, bool RequiresIdleEnvironment = false);
    private sealed record UiTestResult(string Name, bool Passed, long DurationMilliseconds, string? Error, Dictionary<string, object?> Evidence);
}
