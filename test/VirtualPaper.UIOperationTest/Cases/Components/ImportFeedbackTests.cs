using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Moq;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage.Adapter;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UIComponent.Feedback;
using VirtualPaper.UIComponent.Others;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.Adapter.Interfaces;
using VirtualPaper.WpSettingsPanel;
using VirtualPaper.WpSettingsPanel.Utils;
using VirtualPaper.WpSettingsPanel.ViewModels;
using VirtualPaper.UIOperationTest.Infrastructure;
using Windows.Storage;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class ImportFeedbackTests {
    public static async Task RunAsync(UiTestContext context) {
        await LanguageUtil.SetLanguageAsync("en-US");
        var previousContext = ArcPageContextManager.GetContext<WpSettings>();
        var loadingPage = new LoadingPage();
        var window = new FeedbackTestWindow(loadingPage);
        var settings = new Mock<ISettings>();
        settings.SetupGet(s => s.WallpaperDir).Returns(Directory.CreateDirectory(Path.Combine(context.Root, "library")).FullName);
        settings.SetupGet(s => s.BuiltInWallpapersDir).Returns(Directory.CreateDirectory(Path.Combine(context.Root, "built-ins")).FullName);
        settings.SetupGet(s => s.WallpaperArrangement).Returns(WallpaperArrangement.Per);
        var user = new Mock<IUserSettingsClient>(); user.SetupGet(client => client.Settings).Returns(settings.Object);
        var monitors = new Mock<IMonitorManagerClient>();
        monitors.SetupGet(client => client.Monitors).Returns(new System.Collections.ObjectModel.ReadOnlyCollection<IMonitor>([]));
        var core = new Mock<IWallpaperControlClient>();
        using var panel = new WpSettingsViewModel(monitors.Object, core.Object, user.Object, new Mock<IStoragePicker>().Object);
        var index = new WallpaperIndexService(Path.Combine(context.Root, "organization.json"));
        var dialogs = new Mock<IGlobalDialogService>();
        var library = new LibraryContentsViewModel(user.Object, core.Object, panel, index, dialogs.Object);
        Func<string, FileType, CancellationToken, Task<Grpc_WpBasicData?>> create = null!;
        core.Setup(client => client.CreateBasicDataAsync(It.IsAny<string>(), It.IsAny<FileType>(), It.IsAny<CancellationToken>(), false))
            .Returns((string path, FileType type, CancellationToken token, bool _) => create(path, type, token));
        int identity = 0;
        async Task<Grpc_WpBasicData?> Succeed(string path, FileType type, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            string uid = "import-feedback-" + ++identity;
            string folder = Directory.CreateDirectory(Path.Combine(settings.Object.WallpaperDir, uid)).FullName;
            var data = new WpBasicData {
                WallpaperUid = uid, FolderName = uid, FolderPath = folder, FilePath = path, ThumbnailPath = path,
                FType = type, Title = uid, AppInfo = new() { AppVersion = "0.6.1.0", FileVersion = Constants.CoreField.FileVersion }
            };
            await data.SaveMetadataAsync();
            return DataAssist.BasicDataToGrpcData(data);
        }
        try {
            await index.Initialized.Task;
            await window.ShowAsync(context);
            ArcPageContextManager.RegisterContext(new(typeof(WpSettings)), loadingPage.ArcContext);
            create = Succeed;
            var sources = await SourcesAsync(context.Root, 3, "success");
            var originals = sources.ToDictionary(file => file.Path, file => File.ReadAllBytes(file.Path));
            await library.DropFilesAsync(sources);
            await AssertSummaryAsync(context, window, "WpLib_ImportCompletedSummary", 3, InfoBarSeverity.Success);
            Check(Directory.GetDirectories(settings.Object.WallpaperDir).Length == 3, "Import count did not match the stored results");

            // A later operation must have its own message, even with the same severity.
            await library.DropFilesAsync([]);
            await context.WaitAsync(() => window.InfobarMessages.Count == 2, "Independent import summaries");
            Check(window.InfobarMessages.Any(message => message.Message == Summary("WpLib_ImportCompletedSummary", 0)
                && message.Severity == InfoBarSeverity.Informational), "Zero-result import was reported as a success");
            await ClearAsync(context, window);

            foreach (int completedBeforeCancel in new[] { 0, 1 }) {
                var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                int calls = 0;
                create = async (path, type, token) => {
                    if (++calls <= completedBeforeCancel) return await Succeed(path, type, token);
                    entered.SetResult();
                    await Task.Delay(Timeout.Infinite, token);
                    throw new InvalidOperationException("Canceled request unexpectedly resumed");
                };
                Task importing = library.DropFilesAsync(sources);
                try {
                    await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    Check(loadingPage.Overlay.CtsToken != null && loadingPage.Overlay.Visibility == Visibility.Visible,
                        "Production loading control did not expose the active import cancellation");
                    var cancel = FeedbackTestWindow.Descendants<Button>(loadingPage.Overlay).Single(button => button.Name == "BtnCancel");
                    Invoke(cancel);
                    await importing.WaitAsync(TimeSpan.FromSeconds(5));
                }
                finally {
                    loadingPage.Overlay.CtsToken?.Cancel();
                    await importing.WaitAsync(TimeSpan.FromSeconds(5));
                }
                await AssertSummaryAsync(context, window, "WpLib_ImportCanceledSummary", completedBeforeCancel, InfoBarSeverity.Informational);
                Check(calls == completedBeforeCancel + 1, "Cancellation attempted a later source file");
                Check(loadingPage.Overlay.Visibility == Visibility.Collapsed && loadingPage.Overlay.CtsToken == null,
                    "Cancellation retained loading state");
                Check(loadingPage.ArcContext.KeepAliveBlocking.WaitAsync().IsCompleted,
                    "Canceled import retained its page blocking handle");
                await ClearAsync(context, window);
            }

            var failures = await SourcesAsync(context.Root, 40, "long-source-name-" + new string('x', 64));
            var inputs = failures.Append(sources[0]).ToArray();
            create = (path, type, token) => path == sources[0].Path ? Succeed(path, type, token) : Task.FromResult<Grpc_WpBasicData?>(null);
            await library.DropFilesAsync(inputs);
            await context.WaitAsync(() => window.InfobarMessages.Count == 2, "One count and one error summary");
            Check(window.InfobarMessages.Count(message => message.Severity == InfoBarSeverity.Error) == 1,
                "A single import created repeated error bars");
            Check(window.InfobarMessages.Single(message => message.Severity == InfoBarSeverity.Success).Message
                == Summary("WpLib_ImportCompletedSummary", 1), "Failed sources inflated the success count");
            foreach (var theme in new[] { ElementTheme.Light, ElementTheme.Dark }) {
                window.ContentHost.AppRoot.RequestedTheme = theme;
                foreach (var size in new[] { (Width: 900d, Height: 700d), (Width: 800d, Height: 520d) }) {
                    window.Width = size.Width;
                    window.Height = size.Height;
                    await context.WaitForLayoutAsync();
                    await VerifyDetailsAsync(context, window, failures.Select(file => file.Path).ToArray());
                }
            }
            foreach (var (path, bytes) in originals) Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Feedback/import modified an original source");
            await ClearAsync(context, window);
            await VerifyImmediateMessagesAsync(context, window);
            context.Evidence["ImportCountsSuccessZeroAndCancellation"] = new[] { 3, 0, 0, 1 };
            context.Evidence["PartialFailureCount"] = 1;
            context.Evidence["BatchedErrorCount"] = failures.Length;
            context.Evidence["DetailsScrolledAndClosed"] = true;
            context.Evidence["FixtureKind"] = "Production controls and import flow with controlled RPC replies";
        }
        catch {
            context.Evidence["FeedbackMessagesAtFailure"] = window.InfobarMessages.Select(message => new { message.Message, message.Severity }).ToArray();
            context.Evidence["LoadingAtFailure"] = new { loadingPage.Overlay.Visibility, loadingPage.Overlay.CancelEnable, loadingPage.Overlay.CurValue, loadingPage.Overlay.TotalValue };
            string screenshot = Path.Combine(Constants.CommonPaths.LogDir, "UIOperation", "import-feedback-failure.png");
            WindowSnapshot.Save(WinRT.Interop.WindowNative.GetWindowHandle(window), screenshot);
            context.Evidence["FeedbackFailureScreenshot"] = screenshot;
            throw;
        }
        finally {
            ArcPageContextManager.UnregisterContext(new(typeof(WpSettings)));
            if (previousContext != null) ArcPageContextManager.RegisterContext(new(typeof(WpSettings)), previousContext);
            PanelMessageCenter.UnregisterAllActions(PanelContracts.WpSettings.Id);
            LocalizationTestPage.ReleaseLocalization(loadingPage);
            window.Close();
        }
    }

    private static string Summary(string key, int count) => string.Format(LanguageUtil.GetI18n(key), count);

    private static async Task AssertSummaryAsync(UiTestContext context, FeedbackTestWindow window, string key, int count, InfoBarSeverity severity) {
        await context.WaitAsync(() => window.InfobarMessages.Count == 1
            && FeedbackTestWindow.Descendants<InfoBar>(window.Surface).Count(bar => bar.IsOpen) == 1, "Render a single import result bar");
        var bar = FeedbackTestWindow.Descendants<InfoBar>(window.Surface).Single(bar => bar.IsOpen);
        Check(bar.Message == Summary(key, count) && bar.Severity == severity, "Import result message/count/severity is incorrect");
    }

    private static async Task ClearAsync(UiTestContext context, FeedbackTestWindow window) {
        GlobalMessageUtil.ClearAll(window);
        await context.WaitAsync(() => window.InfobarMessages.Count == 0, "Close previous operation messages");
    }

    private static async Task VerifyDetailsAsync(UiTestContext context, FeedbackTestWindow window, string[] paths) {
        await context.WaitAsync(() => FeedbackTestWindow.Descendants<InfoBar>(window.Surface).Count(bar => bar.IsOpen) == 2);
        var bar = FeedbackTestWindow.Descendants<InfoBar>(window.Surface).Single(bar => bar.Severity == InfoBarSeverity.Error);
        Pattern<IInvokeProvider>(bar.ActionButton!, PatternInterface.Invoke).Invoke();
        ContentDialog? dialog = null;
        await context.WaitAsync(() => {
            dialog = VisualTreeHelper.GetOpenPopupsForXamlRoot(window.Content.XamlRoot).SelectMany(popup => popup.Child is ContentDialog rootDialog
                ? new[] { rootDialog } : FeedbackTestWindow.Descendants<ContentDialog>(popup.Child))
                .FirstOrDefault();
            return dialog?.IsLoaded == true;
        }, "Open batch-message details");
        var list = FeedbackTestWindow.Descendants<ListView>(dialog!).Single();
        Check(dialog!.RequestedTheme == window.ContentHost.AppRoot.RequestedTheme, "Message details use the wrong theme");
        var messages = ((IEnumerable<string>)list.ItemsSource).ToArray();
        Check(messages.Length == paths.Length && paths.All(path => messages.Any(message => message.Contains(path, StringComparison.Ordinal))),
            "Batch details lost or duplicated a failed file");
        Check(list.ItemsPanelRoot is ItemsStackPanel, "Batch details lost list virtualization");
        Check(list.ContainerFromIndex(paths.Length - 1) == null, "Batch details eagerly realized the entire long list");
        list.ScrollIntoView(list.Items[^1]);
        await context.WaitAsync(() => list.ContainerFromIndex(paths.Length - 1) is ListViewItem, "Scroll to the last error");
        var last = FeedbackTestWindow.Descendants<TextBlock>((ListViewItem)list.ContainerFromIndex(paths.Length - 1)).Single();
        Check(last.Text == messages[^1] && last.IsTextSelectionEnabled, "Long error text cannot be inspected/copied");
        nint owner = WinRT.Interop.WindowNative.GetWindowHandle(window);
        await Task.Run(() => {
            var root = System.Windows.Automation.AutomationElement.FromHandle(owner);
            var close = ApplicationUi.DialogButton(root, "PrimaryButton");
            Check(root.Current.BoundingRectangle.Contains(close.Current.BoundingRectangle), "Details close button is clipped outside the window");
            Check(!close.Current.IsOffscreen && close.Current.IsEnabled, "Details close button is not available");
        });
        Invoke(FeedbackTestWindow.Descendants<Button>(dialog!).Single(button => button.Name == "PrimaryButton"));
        await context.WaitAsync(() => !dialog!.IsLoaded, "Close batch-message details");
    }

    private static async Task VerifyImmediateMessagesAsync(UiTestContext context, FeedbackTestWindow window) {
        bool clicked = false;
        bool updatedClicked = false;
        using (GlobalMessageUtil.BeginBatch()) {
            GlobalMessageUtil.ShowSuccess("Action remains available", "Inspect", () => clicked = true, arcWindow: window, key: "Feedback.Action");
            await context.WaitAsync(() => FeedbackTestWindow.Descendants<InfoBar>(window.Surface).Any(bar => bar.IsOpen));
            var bar = FeedbackTestWindow.Descendants<InfoBar>(window.Surface).Single(bar => bar.IsOpen);
            Pattern<IInvokeProvider>(bar.ActionButton!, PatternInterface.Invoke).Invoke();
            Check(clicked, "Batching lost an existing action callback");
            GlobalMessageUtil.ShowSuccess("Updated action", "Inspect updated", () => updatedClicked = true,
                replaceExisting: true, arcWindow: window, key: "Feedback.Action");
            await context.WaitAsync(() => FeedbackTestWindow.Descendants<InfoBar>(window.Surface).Count(info => info.IsOpen) == 1
                && FeedbackTestWindow.Descendants<InfoBar>(window.Surface).Single(info => info.IsOpen).Message == "Updated action",
                "Replace the existing message without creating a second bar");
            var updated = FeedbackTestWindow.Descendants<InfoBar>(window.Surface).Single(info => info.IsOpen);
            Pattern<IInvokeProvider>(updated.ActionButton!, PatternInterface.Invoke).Invoke();
            Check(updatedClicked, "Replacing the message retained its old callback");
        }
        await ClearAsync(context, window);
        using (GlobalMessageUtil.BeginBatch()) {
            GlobalMessageUtil.ShowAutoCloseMessage("Temporary feedback", InfoBarSeverity.Informational, autoCloseDelay: 1500, arcWindow: window);
            await context.WaitAsync(() => window.InfobarMessages.Count == 1, "Show automatic-close feedback during a batch");
            await context.WaitAsync(() => window.InfobarMessages.Count == 0, "Automatic feedback expires before the batch ends");
        }
        Check(window.InfobarMessages.Count == 0, "Finishing the batch revived an expired message");
        using (GlobalMessageUtil.BeginBatch()) {
            GlobalMessageUtil.ShowWarning("Transient state", arcWindow: window, key: "Feedback.Transient");
            GlobalMessageUtil.CloseAndRemoveMsg(window, "Feedback.Transient");
        }
        await context.WaitForLayoutAsync();
        Check(window.InfobarMessages.Count == 0, "Finishing the batch revived a withdrawn state");
    }

    private static async Task<StorageFile[]> SourcesAsync(string root, int count, string prefix) {
        var files = new List<StorageFile>();
        for (int i = 0; i < count; i++) {
            string path = Path.Combine(root, prefix + "-" + i + ".png");
            WallpaperLibraryTests.WriteImage(path, 8, 8);
            files.Add(await StorageFile.GetFileFromPathAsync(path));
        }
        return files.ToArray();
    }

    private sealed class LoadingPage : ArcPage {
        public override Type ArcType => typeof(LoadingPage);
        internal Loading Overlay { get; } = new() { Visibility = Visibility.Collapsed };
        internal LoadingPage() {
            Content = Overlay;
            ArcContext.AttachLoadingComponent(Overlay);
        }
    }
}
