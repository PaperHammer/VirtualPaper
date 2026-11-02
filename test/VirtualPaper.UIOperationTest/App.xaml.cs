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
            || (commandLine.Length == 5 && commandLine[3] != "--module")) {
            Environment.ExitCode = 2;
            Exit();
            return;
        }

        var cases = new[] {
            new UiTestCase("GeneralEffect.RImage.EditRestoreSaveAndReopen",
                context => GeneralEffectTests.EditRestoreSaveAndReopenAsync(context, RuntimeType.RImage)),
            new UiTestCase("GeneralEffect.RImage3D.EditRestoreSaveAndReopen",
                context => GeneralEffectTests.EditRestoreSaveAndReopenAsync(context, RuntimeType.RImage3D)),
            new UiTestCase("Application.NavigationSettings.SaveAndReopen", NavigationSettingsTests.SaveAndReopenAsync),
            new UiTestCase("Application.PerformanceSettings.SaveAndReopen", PerformanceSettingsTests.SaveAndReopenAsync),
            new UiTestCase("Application.WallpaperLibrary.ImportApplyCloseDelete", WallpaperLibraryTests.ImportApplyCloseDeleteAsync),
            new UiTestCase("Application.ScreenSaver.SettingsAndWhitelist", ScreenSaverTests.SettingsAndWhitelistAsync),
            new UiTestCase("Application.ScreenFit.PreviewCancelApplyAndReopen", ScreenFitTests.PreviewCancelApplyAndReopenAsync)
        };
        if (commandLine.Length == 5)
            cases = cases.Where(test => test.Name.StartsWith("Application." + commandLine[4] + ".", StringComparison.Ordinal)).ToArray();
        var results = new List<UiTestResult>();
        try {
            CrossThreadInvoker.Initialize(new UiSynchronizationContext());
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
                catch (Exception ex) { error = ex.ToString(); }
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
                Exit();
            }
        }
    }

    private sealed record UiTestCase(string Name, Func<UiTestContext, Task> Run);
    private sealed record UiTestResult(string Name, bool Passed, long DurationMilliseconds, string? Error, Dictionary<string, object?> Evidence);
}
