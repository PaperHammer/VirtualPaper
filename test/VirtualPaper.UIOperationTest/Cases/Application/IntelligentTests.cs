using System.Security.Cryptography;
using System.Windows.Automation;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class IntelligentTests {
    public static async Task ConfigureCancelAndReopenAsync(UiTestContext context) {
        string source = Path.Combine(context.Root, "intelligent-source.png");
        string style = Path.Combine(context.Root, "intelligent-style.png");
        WallpaperLibraryTests.WriteImage(source, 256, 144);
        WallpaperLibraryTests.WriteImage(style, 144, 256);
        byte[] sourceHash = SHA256.HashData(await File.ReadAllBytesAsync(source));
        byte[] styleHash = SHA256.HashData(await File.ReadAllBytesAsync(style));
        await using var session = new AppProcessSession();
        await session.StartAsync();
        context.Evidence["CoreProcessId"] = session.CoreId;
        var ui = await session.OpenUiAsync();
        nint ownerWindow = ui.MainWindowHandle;
        var root = await Task.Run(() => {
            var window = AutomationElement.FromHandle(ownerWindow);
            ApplicationUi.Select(ApplicationUi.Find(window, "App.Navigation.Intelligent"));
            ApplicationUi.Select(ApplicationUi.Find(window, "Intelligent.Tab.SuperResolution"));
            OpenTask(window);
            CheckConfirm(window, false);
            ApplicationUi.Invoke(ApplicationUi.Find(window, "Intelligent.SuperResolution.Source"));
            NativeFileDialog.Cancel(ui, ownerWindow);
            CheckConfirm(window, false);
            ApplicationUi.Invoke(ApplicationUi.Find(window, "Intelligent.SuperResolution.Source"));
            NativeFileDialog.OpenFile(ui, ownerWindow, source);
            return window;
        });
        await WaitForSizeAsync(root, "512 * 288");
        await Task.Run(() => {
            CheckConfirm(root, true);
            ApplicationUi.Select(ApplicationUi.Find(root, "Intelligent.SuperResolution.4x"));
        });
        await WaitForSizeAsync(root, "1024 * 576");
        await Task.Run(() => ApplicationUi.Select(ApplicationUi.Find(root, "Intelligent.SuperResolution.Restore")));
        await WaitForSizeAsync(root, "256 * 144");
        await Task.Run(() => ApplicationUi.Select(ApplicationUi.Find(root, "Intelligent.SuperResolution.Enhance")));
        await WaitForSizeAsync(root, "1024 * 576");
        await Task.Run(async () => {
            CancelTask(root);
            OpenTask(root);
            CheckConfirm(root, false);
            var twoTimes = ApplicationUi.Find(root, "Intelligent.SuperResolution.2x");
            Check(((SelectionItemPattern)twoTimes.GetCurrentPattern(SelectionItemPattern.Pattern)).Current.IsSelected,
                "Cancelled enhancement magnification was not reset");
            CancelTask(root);
            ApplicationUi.Select(ApplicationUi.Find(root, "Intelligent.Tab.StyleTransfer"));
            OpenTask(root);
            CheckConfirm(root, false);
            ApplicationUi.Invoke(ApplicationUi.Find(root, "Intelligent.StyleTransfer.Style"));
            NativeFileDialog.OpenFile(ui, ownerWindow, style);
            await WaitForSelectedPathAsync(root, "Intelligent.StyleTransfer.Style", style);
            CheckConfirm(root, false);
            ApplicationUi.Invoke(ApplicationUi.Find(root, "Intelligent.StyleTransfer.Source"));
            NativeFileDialog.OpenFile(ui, ownerWindow, source);
            await WaitForSelectedPathAsync(root, "Intelligent.StyleTransfer.Source", source);
            CheckConfirm(root, true);
            CancelTask(root);
            OpenTask(root);
            CheckConfirm(root, false);
            CancelTask(root);
        });
        context.Evidence["InputValidationAndCancellationVerified"] = true;
        await VerifyLocalizedFormsAsync(context, ui, root);
        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        await Task.Run(() => {
            root = AutomationElement.FromHandle(ui.MainWindowHandle);
            ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Intelligent"));
            OpenTask(root);
            CheckConfirm(root, false);
            CancelTask(root);
            ApplicationUi.Select(ApplicationUi.Find(root, "Intelligent.Tab.SuperResolution"));
            OpenTask(root);
            CheckConfirm(root, false);
            CancelTask(root);
        });
        Check(sourceHash.SequenceEqual(SHA256.HashData(await File.ReadAllBytesAsync(source))), "Task configuration modified the source image");
        Check(styleHash.SequenceEqual(SHA256.HashData(await File.ReadAllBytesAsync(style))), "Task configuration modified the style image");
        await session.StopAsync();
        session.RecordEvidence(context);
        Check(((string[])context.Evidence["RuntimeErrors"]!).Length == 0, "Intelligent configuration logged a runtime error; inspect report evidence");
        context.Evidence["ConfigureCancelAndReopenVerified"] = true;
        context.Evidence["ModelInferenceAndQueueTested"] = false;
    }

    private static void OpenTask(AutomationElement root) {
        ApplicationUi.Invoke(ApplicationUi.Find(root, "Intelligent.AddTask"));
        ApplicationUi.Find(root, "Intelligent.Task.Cancel", visible: true);
    }

    private static async Task VerifyLocalizedFormsAsync(UiTestContext context, System.Diagnostics.Process ui, AutomationElement root) {
        var initial = await Task.Run(() => root.Current.BoundingRectangle);
        var samples = new List<object>();
        foreach (string locale in new[] { "de-DE", "fr-FR", "ru-RU" }) {
            await ApplicationUi.SelectLanguageAsync(root, locale);
            foreach (int width in new[] { 1440, 800 }) {
                await Task.Run(() => ApplicationUi.ResizeOwnedWindow(ui.MainWindowHandle, width, 850));
                foreach (string tab in new[] { "Intelligent.Tab.StyleTransfer", "Intelligent.Tab.SuperResolution" }) {
                    await Task.Run(() => {
                        ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Intelligent"));
                        ApplicationUi.Select(ApplicationUi.Find(root, tab));
                        OpenTask(root);
                    });
                    await AppProcessSession.WaitAsync(() => Task.Run(() => {
                        var bounds = root.Current.BoundingRectangle;
                        var cancel = ApplicationUi.Find(root, "Intelligent.Task.Cancel", visible: true).Current.BoundingRectangle;
                        var confirm = ApplicationUi.Find(root, "Intelligent.Task.Confirm", visible: true, enabled: false).Current.BoundingRectangle;
                        return cancel.Width >= 100 && confirm.Width >= 100 && bounds.Contains(cancel) && bounds.Contains(confirm)
                            && Math.Abs(cancel.Top - confirm.Top) < 2 && cancel.Right <= confirm.Left;
                    }), "Localized task footer " + locale);
                    await Task.Run(() => CancelTask(root));
                    samples.Add(new { Locale = locale, Width = width, Tab = tab });
                }
            }
        }
        await ApplicationUi.SelectLanguageAsync(root, "en-US");
        await Task.Run(() => {
            // UIA bounds are physical pixels; restore the original outer size directly.
            ApplicationUi.ResizeOwnedWindow(ui.MainWindowHandle, (int)initial.Width, (int)initial.Height, physicalPixels: true);
            ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Intelligent"));
        });
        context.Evidence["LocalizedFormFooterSamples"] = samples;
    }

    private static void CancelTask(AutomationElement root) {
        ApplicationUi.Invoke(ApplicationUi.Find(root, "Intelligent.Task.Cancel", visible: true));
        ApplicationUi.WaitUntilGone(root, "Intelligent.Task.Cancel");
    }

    private static void CheckConfirm(AutomationElement root, bool enabled) =>
        Check(ApplicationUi.Find(root, "Intelligent.Task.Confirm", visible: true, enabled: false).Current.IsEnabled == enabled,
            $"Intelligent task confirmation enabled state should be {enabled}");

    private static Task WaitForSizeAsync(AutomationElement root, string size) => AppProcessSession.WaitAsync(() =>
        Task.Run(() => ApplicationUi.Find(root, "Intelligent.SuperResolution.OutputSize", visible: true).Current.Name == size),
        "Enhancement output size " + size);

    private static Task WaitForSelectedPathAsync(AutomationElement root, string id, string path) => AppProcessSession.WaitAsync(() =>
        Task.Run(() => string.Equals(ApplicationUi.Find(root, id, visible: true).Current.HelpText, path, StringComparison.OrdinalIgnoreCase)),
        "Selected image path " + id);
}
