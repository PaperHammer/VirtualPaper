using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows.Automation;
using Google.Protobuf.WellKnownTypes;
using VirtualPaper.UIOperationTest.Infrastructure;
using Workloads.Creation.StaticImg.Models.SerializableData;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class EditorExitTests {
    public static Task ImageAsync(UiTestContext context) => RunAsync(context, web: false);
    public static Task WebAsync(UiTestContext context) => RunAsync(context, web: true);

    private static async Task RunAsync(UiTestContext context, bool web) {
        string source = Path.Combine(context.Root, "exit-source.png");
        string design = Path.Combine(context.Root, "saved-on-exit.vpd");
        WallpaperLibraryTests.WriteImage(source);
        byte[] hash = SHA256.HashData(await File.ReadAllBytesAsync(source));
        await using var session = new AppProcessSession();
        await session.StartAsync();
        context.Evidence["CoreProcessId"] = session.CoreId;
        var ui = await session.OpenUiAsync();
        var root = await Task.Run(() => web ? CreateWebProject(ui, context.Root)
            : StaticEditorTests.OpenImage(ui, ui.MainWindowHandle, source));
        string? entry = web ? Path.Combine(context.Root, "exit-web", "index.html") : null;
        string original = entry != null ? await File.ReadAllTextAsync(entry) : "";
        if (web) {
            await Task.Run(() => ApplicationUi.ClickPointerElement(ui.MainWindowHandle, WebEditorTests.FindItem(root, "index.html")));
            await WebEditorTests.WaitForEditorStatusAsync(root, "Saved");
            await Task.Run(() => ApplicationUi.TypeAtEditorSurface(ui.MainWindowHandle,
                ApplicationUi.Find(root, "WebEditor.CodeSurface", visible: true), "<!-- EXIT_UNSAVED_EDIT -->"));
            await WebEditorTests.WaitForEditorStatusAsync(root, "Unsaved");
        }
        else await Task.Run(() => ApplicationUi.Invoke(ApplicationUi.Find(root, "StaticEditor.RotateLeft")));

        // Window-close cancellation must leave later IPC and theme changes usable.
        await Task.Run(() => {
            Check(ui.CloseMainWindow(), "Dirty editor did not accept a window-close request");
            ApplicationUi.Invoke(ApplicationUi.DialogButton(root, "CloseButton"));
            ApplicationUi.WaitUntilGone(root, "PrimaryButton");
        });
        Check(!ui.HasExited, "Cancelling window close terminated the editor");

        // Web uses the real tray action, including cancellation and retry.
        // Core must show the unsaved dialog even though IPC arrives off-thread.
        await RequestExitAsync(session, web);
        await Task.Run(() => {
            ApplicationUi.Invoke(ApplicationUi.DialogButton(root, "CloseButton"));
            ApplicationUi.WaitUntilGone(root, "PrimaryButton");
        });
        Check(!ui.HasExited, "Cancelling Core exit terminated the editor");
        await session.WaitForShutdownCancellationAsync(1);
        await session.Settings.GetSettingsAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(2));
        context.Evidence["WindowAndCoreExitCancelled"] = true;
        if (web) context.Evidence["TrayExitCancelledAndEditorRemainsUsable"] = true;

        if (!web) {
            await session.RequestShutdownAsync();
            await Task.Run(() => {
                ApplicationUi.Invoke(ApplicationUi.Find(root, "PrimaryButton", visible: true));
                NativeFileDialog.Cancel(ui, ui.MainWindowHandle);
            });
            Check(!ui.HasExited, "Cancelling the save picker closed the dirty editor");
            await session.WaitForShutdownCancellationAsync(2);
            context.Evidence["SavePickerCancellationKeepsEditorOpen"] = true;
        }

        if (web) {
            await WebEditorTests.WaitForEditorStatusAsync(root, "Unsaved");
            Check(await File.ReadAllTextAsync(entry!) == original, "Cancelled exit saved the Web edit unexpectedly");
        }
        await RequestExitAsync(session, web);
        await Task.Run(() => {
            ApplicationUi.Invoke(ApplicationUi.Find(root, web ? "SecondaryButton" : "PrimaryButton", visible: true));
            if (!web) NativeFileDialog.SaveFile(ui, ui.MainWindowHandle, design);
        });
        await session.WaitForShutdownAsync();
        session.RecordEvidence(context);
        Check(((string[])context.Evidence["RuntimeErrors"]!).Length == 0, "Editor exit logged a runtime error");
        Check(hash.SequenceEqual(SHA256.HashData(await File.ReadAllBytesAsync(source))), "Exit modified the original image");
        if (web) Check(await File.ReadAllTextAsync(entry!) == original, "Discarding exit saved the unsaved Web edit");
        else {
            var header = await StaticImgDesignFileUtil.GetFileHeaderAsync(design);
            Check(header is { CanvasWidth: 1080, CanvasHeight: 1920 }, "Save on exit lost the edited canvas");
        }
        context.Evidence["ApprovedExitClosesUiAndCoreNormally"] = true;
        context.Evidence["ExitAction"] = web ? "Discard" : "Save";
        context.Evidence["ExitEntry"] = web ? "Tray menu Exit" : "Core shutdown RPC";
        context.Evidence["Editor"] = web ? "Web" : "Image";
    }

    private static Task RequestExitAsync(AppProcessSession session, bool web) => web ? Task.Run(() => {
        var menu = TrayUi.Open(session.CoreId);
        ApplicationUi.Invoke(ApplicationUi.Find(menu.Element, "Tray.Exit", visible: true));
    }) : session.RequestShutdownAsync();

    private static AutomationElement CreateWebProject(Process ui, string location) {
        var root = AutomationElement.FromHandle(ui.MainWindowHandle);
        ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Projects"));
        ApplicationUi.Invoke(ApplicationUi.Find(root, "Projects.New"));
        var templates = ApplicationUi.Find(root, "Projects.Templates").FindAll(TreeScope.Children,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
        Check(templates.Count == 2, "Expected image and Web project templates");
        ApplicationUi.Select(templates[1]);
        ApplicationUi.SetText(ApplicationUi.Find(root, "Projects.Name"), "exit-web");
        ApplicationUi.SetText(ApplicationUi.Find(root, "Projects.Location"), location);
        ApplicationUi.Invoke(ApplicationUi.Find(root, "Projects.Create"));
        ApplicationUi.Find(root, "WebEditor.Files");
        return root;
    }
}
