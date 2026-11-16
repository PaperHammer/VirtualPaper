using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using VirtualPaper.Common;
using VirtualPaper.UIOperationTest.Infrastructure;
using Workloads.Creation.StaticImg;
using Workloads.Creation.StaticImg.Core.Utils;
using Workloads.Creation.StaticImg.Models;
using Workloads.Creation.StaticImg.Models.SerializableData;
using Workloads.Creation.StaticImg.Models.Specific;
using Workloads.Creation.WebBackdrop.Models.SerializableData;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class ProjectActivationTests {
    public static async Task RunAsync(UiTestContext context) {
        string image = Path.Combine(context.Root, "外部打开 图片.vpd");
        string web = Path.Combine(context.Root, "web project", "外部 Web.vpw");
        Directory.CreateDirectory(Path.GetDirectoryName(web)!);
        using (var ink = new InkProjectSession(image, FileType.FImage)) {
            var size = new ArcSize(72, 48, 96, RebuildMode.None);
            using var render = new InkRenderData(ink, size);
            render.RenderTarget.SetPixelBytes(Enumerable.Range(0, 72 * 48)
                .SelectMany(i => new byte[] { (byte)(i % 200), 110, 180, 255 }).ToArray());
            var business = new BusinessData { SelectedLayerIndex = 0 };
            business.SetColors([Windows.UI.Color.FromArgb(255, 20, 80, 140)]);
            Check((await ink.DesignFileUtil.SaveAsync(size, business,
                [new Layer("Activation fixture", new LayerState { IsVisible = true }, render)])).Success,
                "Could not create the saved image project fixture.");
        }
        WebDesignFileUtil.Create(web).EnsureProjectStructure();
        await context.ActivateHostAsync();
        await using var session = new AppProcessSession();
        await session.StartAsync(settings => settings.UiHardwareAccelerationEnabled = false, projectPaths: [image]);
        var ui = await session.WaitForUiAsync(firstRun: true, activateWindow: false);
        await AppProcessSession.WaitAsync(() => Task.Run(() => AutomationElement.RootElement.FindFirst(TreeScope.Children,
            new AndCondition(new PropertyCondition(AutomationElement.ProcessIdProperty, session.CoreId),
                new PropertyCondition(AutomationElement.NameProperty, "SplashWindow"))) == null), "Close the startup splash after UI appears");
        context.Evidence["NoStartupSplashLeftAfterUiVisible"] = true;
        await WaitForegroundAsync(ui.MainWindowHandle);
        var root = AutomationElement.FromHandle(ui.MainWindowHandle);
        await WaitTabsAsync(root, image, web, 1);
        await Task.Run(() => {
            ApplicationUi.Select(ApplicationUi.Find(root, "StaticEditor.Tool.CanvasSet"));
            ApplicationUi.Expand(ApplicationUi.Find(root, "StaticEditor.Canvas"));
            ApplicationUi.Invoke(ApplicationUi.Find(root, "StaticEditor.RotateLeft"));
        });
        await WaitDimensionsAsync(root, 48, 72);

        int originalUi = ui.Id;
        await context.ActivateHostAsync();
        await session.ForwardProjectFilesAsync(web, image);
        await WaitForegroundAsync(ui.MainWindowHandle);
        await WaitTabsAsync(root, image, web, 2);
        await WaitDimensionsAsync(root, 48, 72);
        Check(!ui.HasExited && ui.Id == originalUi, "Forwarding replaced the running editor.");
        Check((await StaticImgDesignFileUtil.GetFileHeaderAsync(image)) is { CanvasWidth: 72, CanvasHeight: 48 },
            "Opening another project silently saved the dirty canvas.");
        await context.ActivateHostAsync();
        await session.ForwardProjectFilesAsync(web, web);
        await WaitForegroundAsync(ui.MainWindowHandle);
        await AppProcessSession.WaitAsync(() => Task.Run(() => Tabs(root, image).Count + Tabs(root, web).Count == 2
            && ((SelectionItemPattern)Tabs(root, web).Single().GetCurrentPattern(SelectionItemPattern.Pattern)).Current.IsSelected),
            "Repeated Web opening activates its existing tab");
        await context.ActivateHostAsync();
        await session.ForwardProjectFilesAsync(image);
        await WaitForegroundAsync(ui.MainWindowHandle);
        await WaitDimensionsAsync(root, 48, 72);
        await Task.Run(() => {
            var tab = Tabs(root, image).Single();
            Check(((SelectionItemPattern)tab.GetCurrentPattern(SelectionItemPattern.Pattern)).Current.IsSelected,
                "Repeated opening did not activate the existing image tab.");
            ApplicationUi.Expand(ApplicationUi.Find(root, "Project.File"));
            ApplicationUi.Invoke(ApplicationUi.Find(root, "Project.File.Save", visible: true));
        });
        await AppProcessSession.WaitAsync(async () =>
            (await StaticImgDesignFileUtil.GetFileHeaderAsync(image)) is { CanvasWidth: 48, CanvasHeight: 72 }, "Explicitly save the preserved edit");
        context.Evidence["ForwardedBatchReusesUiAndPreservesDirtyImage"] = true;

        await session.CloseUiAsync();
        await context.ActivateHostAsync();
        await session.ForwardProjectFilesAsync(web);
        ui = await session.WaitForUiAsync(firstRun: true, activateWindow: false);
        await WaitForegroundAsync(ui.MainWindowHandle);
        Check(ui.Id != originalUi, "Opening with UI closed did not create a new UI.");
        root = AutomationElement.FromHandle(ui.MainWindowHandle);
        await WaitTabsAsync(root, image, web, 1);
        await Task.Run(() => Check(Tabs(root, web).Count == 1 && Tabs(root, image).Count == 0, "UI reopened projects that were not requested."));
        await Task.Run(() => CloseTab(Tabs(root, web).Single()));
        await AppProcessSession.WaitAsync(() => Task.Run(() =>
            ApplicationUi.Find(root, "Projects.New", visible: true).Current.IsEnabled), "Return to project start page");
        await context.ActivateHostAsync();
        await session.ForwardProjectFilesAsync(image);
        await WaitForegroundAsync(ui.MainWindowHandle);
        await WaitTabsAsync(root, image, web, 1);
        await Task.Run(() => Check(Tabs(root, web).Count == 0 && Tabs(root, image).Count == 1,
            "External opening reused a stale navigation payload."));
        await session.StopAsync();
        session.RecordEvidence(context);
        Check(((string[])context.Evidence["RuntimeErrors"]!).Length == 0, "Project activation logged runtime errors.");
        context.Evidence["ColdCoreActivation"] = PackageActivation.ExpectedFullName != null || InstalledProjectActivation.IsEnabled
            ? "WindowsShellAssociation" : "ProjectArguments";
        context.Evidence["UiClosedActivationAndClearedWorkspaceVerified"] = true;
        context.Evidence["ExplorerRegistrationExercised"] = PackageActivation.ExpectedFullName != null || InstalledProjectActivation.IsEnabled;
    }

    private static List<AutomationElement> Tabs(AutomationElement root, string path) => root.FindAll(TreeScope.Descendants,
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem)).Cast<AutomationElement>()
        .Where(tab => tab.Current.Name.Contains(Path.GetFileName(path), StringComparison.Ordinal)
            || tab.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, Path.GetFileName(path))) != null).ToList();

    private static Task WaitTabsAsync(AutomationElement root, string image, string web, int count) =>
        AppProcessSession.WaitAsync(() => Task.Run(() => Tabs(root, image).Count + Tabs(root, web).Count == count), "Requested project tabs");

    private static Task WaitDimensionsAsync(AutomationElement root, int width, int height) => AppProcessSession.WaitAsync(() => Task.Run(() =>
        double.Parse(((ValuePattern)ApplicationUi.Find(root, "StaticEditor.Width", visible: true).GetCurrentPattern(ValuePattern.Pattern)).Current.Value, CultureInfo.InvariantCulture) == width
        && double.Parse(((ValuePattern)ApplicationUi.Find(root, "StaticEditor.Height", visible: true).GetCurrentPattern(ValuePattern.Pattern)).Current.Value, CultureInfo.InvariantCulture) == height), "Preserved canvas dimensions");

    private static void CloseTab(AutomationElement tab) {
        var close = tab.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))
            .Cast<AutomationElement>().First(button => button.Current.Name.Contains("Close", StringComparison.OrdinalIgnoreCase)
                || button.Current.AutomationId.Contains("Close", StringComparison.OrdinalIgnoreCase));
        ApplicationUi.Invoke(close);
    }

    private static Task WaitForegroundAsync(nint window) => AppProcessSession.WaitAsync(() => Task.FromResult(GetForegroundWindow() == window),
        "Project activation brought its UI to the foreground");
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
}
