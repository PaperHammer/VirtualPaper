using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Windows.Automation;
using VirtualPaper.Common;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class WebEditorTests {
    private const string ProjectName = "ui-web-project";
    private const string Marker = "UI_OPERATION_WEB_EDIT";

    public static async Task CreateFilesSaveReopenAndExportAsync(UiTestContext context) {
        string folder = Path.Combine(context.Root, ProjectName);
        string manifest = Path.Combine(folder, ProjectName + ".vpw");
        string output = Path.Combine(context.Root, "web-project-full.zip");
        await using var session = new AppProcessSession();
        await session.StartAsync();
        context.Evidence["CoreProcessId"] = session.CoreId;
        var ui = await session.OpenUiAsync();
        nint ownerWindow = ui.MainWindowHandle;
        var root = await Task.Run(() => {
            var window = AutomationElement.FromHandle(ownerWindow);
            ApplicationUi.Select(ApplicationUi.Find(window, "App.Navigation.Projects"));
            ApplicationUi.Invoke(ApplicationUi.Find(window, "Projects.New"));
            var templates = ApplicationUi.Find(window, "Projects.Templates");
            var items = templates.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
            Check(items.Count == 2, "Project creation did not display the expected templates");
            ApplicationUi.Select(items[1]);
            ApplicationUi.SetText(ApplicationUi.Find(window, "Projects.Name"), ProjectName);
            ApplicationUi.SetText(ApplicationUi.Find(window, "Projects.Location"), context.Root);
            ApplicationUi.Invoke(ApplicationUi.Find(window, "Projects.Create"));
            try { ApplicationUi.Find(window, "WebEditor.Files"); }
            catch {
                WindowSnapshot.Save(ownerWindow, Path.Combine(Constants.CommonPaths.LogDir, "UIOperation", "web-project-created.png"));
                context.Evidence["InitialUiControls"] = window.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>()
                    .Where(element => !string.IsNullOrEmpty(element.Current.AutomationId) || element.Current.ControlType == ControlType.TreeItem)
                    .Select(element => $"{element.Current.ControlType.ProgrammaticName} {element.Current.AutomationId}: {element.Current.Name} enabled={element.Current.IsEnabled} offscreen={element.Current.IsOffscreen}")
                    .Take(150).ToArray();
                throw;
            }
            return window;
        });
        await AppProcessSession.WaitForJsonAsync(manifest, json => json["name"]?.GetValue<string>() == ProjectName);
        Check(File.Exists(Path.Combine(folder, "index.html")), "Created web project has no entry file");
        context.Evidence["ProjectCreated"] = true;
        await Task.Run(() => {
            CreateItem(root, ownerWindow, "WebEditor.NewFolder", "assets");
            CreateItem(root, ownerWindow, "WebEditor.NewFile", "cancelled.txt", cancel: true);
            CreateItem(root, ownerWindow, "WebEditor.NewFile", "notes.txt");
        });
        await AppProcessSession.WaitAsync(() => Task.FromResult(Directory.Exists(Path.Combine(folder, "assets"))
            && File.Exists(Path.Combine(folder, "notes.txt"))), "Web folder and file created on disk");
        Check(!File.Exists(Path.Combine(folder, "cancelled.txt")), "Cancelling creation added a web file");
        await Task.Run(() => {
            OpenItemMenu(root, ownerWindow, "notes.txt", "WebEditor.DeleteFile");
        });
        await AppProcessSession.WaitAsync(() => Task.FromResult(!File.Exists(Path.Combine(folder, "notes.txt"))), "Web file deleted on disk");
        context.Evidence["CreateCancelAndDeleteVerified"] = true;
        await Task.Run(() => {
            ApplicationUi.ClickPointerElement(ownerWindow, FindItem(root, "index.html"));
        });
        await WaitForEditorStatusAsync(root, "Saved");
        await Task.Run(() => ApplicationUi.TypeAtEditorSurface(ownerWindow,
            ApplicationUi.Find(root, "WebEditor.CodeSurface", visible: true), "<!-- " + Marker + " -->"));
        await WaitForEditorStatusAsync(root, "Unsaved");
        await Task.Run(() => {
            ApplicationUi.Expand(ApplicationUi.Find(root, "Project.File"));
            ApplicationUi.Invoke(ApplicationUi.Find(root, "Project.File.Save", visible: true));
        });
        try {
            await AppProcessSession.WaitAsync(async () => (await AppProcessSession.ReadTextSharedAsync(Path.Combine(folder, "index.html"))).Contains(Marker,
                StringComparison.Ordinal), "Monaco UI edit saved to entry file");
        }
        catch {
            context.Evidence["EntryAfterSave"] = await File.ReadAllTextAsync(Path.Combine(folder, "index.html"));
            await Task.Run(() => WindowSnapshot.Save(ownerWindow,
                Path.Combine(Constants.CommonPaths.LogDir, "UIOperation", "web-editor-after-save.png")));
            throw;
        }
        await AppProcessSession.WaitForJsonAsync(manifest, json => HasPath(json, "assets") && !HasPath(json, "notes.txt"));
        context.Evidence["CodeEditSaved"] = true;
        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        ownerWindow = ui.MainWindowHandle;
        await Task.Run(() => {
            root = AutomationElement.FromHandle(ownerWindow);
            ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Projects"));
            ApplicationUi.Invoke(ApplicationUi.Find(root, "Projects.Open"));
            NativeFileDialog.OpenFile(ui, ownerWindow, manifest);
            FindItem(root, "assets");
            ApplicationUi.ClickPointerElement(ownerWindow, FindItem(root, "index.html"));
            var editor = ApplicationUi.Find(root, "WebEditor.CodeSurface", visible: true);
            Check(string.Equals(editor.Current.Name, Path.Combine(folder, "index.html"), StringComparison.OrdinalIgnoreCase),
                "Reopened editor did not load the entry file path");
        });
        await WaitForEditorStatusAsync(root, "Saved");
        await Task.Run(() => {
            WindowSnapshot.Save(ownerWindow, Path.Combine(Constants.CommonPaths.LogDir, "UIOperation", "web-reopened.png"));
            ApplicationUi.Expand(ApplicationUi.Find(root, "Project.File"));
            ApplicationUi.Expand(ApplicationUi.Find(root, "Project.File.Export", visible: true));
            ApplicationUi.Invoke(ApplicationUi.Find(root, "Project.File.ExportFullZip", visible: true));
            NativeFileDialog.SaveFile(ui, ownerWindow, output);
        });
        await AppProcessSession.WaitAsync(() => {
            try {
                // SaveFilePicker creates an empty destination first. Do not lock
                // that placeholder while the exporter deletes and recreates it.
                if (!File.Exists(output) || new FileInfo(output).Length < 22) return Task.FromResult(false);
                using var archive = ZipFile.OpenRead(output);
                var entry = archive.GetEntry("index.html");
                if (entry == null) return Task.FromResult(false);
                using var reader = new StreamReader(entry.Open());
                Check(reader.ReadToEnd().Contains(Marker, StringComparison.Ordinal), "Exported archive lost the UI code edit");
                Check(archive.GetEntry(ProjectName + ".vpw") != null, "Full archive omitted the web project manifest");
                Check(archive.GetEntry("notes.txt") == null, "Exported archive restored a deleted file");
                context.Evidence["ArchiveEntries"] = archive.Entries.Select(item => item.FullName).ToArray();
                return Task.FromResult(true);
            }
            catch (IOException) { return Task.FromResult(false); }
            catch (InvalidDataException) { return Task.FromResult(false); }
        }, "Web project archive exported");
        await session.StopAsync();
        session.RecordEvidence(context);
        Check(((string[])context.Evidence["RuntimeErrors"]!).Length == 0, "Web editor logged a runtime error; inspect report evidence");
        context.Evidence["CreateFilesSaveReopenAndExportVerified"] = true;
        context.Evidence["RendererTextOcrVerified"] = false;
    }

    private static bool HasPath(JsonNode json, string path) => json["files"]!.AsArray().Any(item =>
        string.Equals(item?["path"]?.GetValue<string>()?.TrimEnd('/'), path, StringComparison.OrdinalIgnoreCase));

    private static void CreateItem(AutomationElement root, nint ownerWindow, string action, string name, bool cancel = false) {
        OpenItemMenu(root, ownerWindow, ProjectName, action);
        ApplicationUi.SetText(ApplicationUi.Find(root, "Dialog.ItemName", visible: true), name);
        ApplicationUi.Invoke(ApplicationUi.Find(root, cancel ? "SecondaryButton" : "PrimaryButton", visible: true));
        ApplicationUi.WaitUntilGone(root, "Dialog.ItemName");
        if (!cancel) FindItem(root, name);
    }

    private static void OpenItemMenu(AutomationElement root, nint ownerWindow, string name, string action) {
        using var foreground = ApplicationUi.Foreground(ownerWindow);
        ApplicationUi.OpenPointerContextMenu(ownerWindow, FindItem(root, name));
        try { ApplicationUi.Invoke(ApplicationUi.Find(root, action, visible: true)); }
        catch {
            string path = Path.Combine(Constants.CommonPaths.LogDir, "UIOperation", "web-file-menu");
            WindowSnapshot.Save(ownerWindow, path + ".png");
            File.WriteAllLines(path + ".txt", root.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>()
                .Where(item => !item.Current.IsOffscreen)
                .Select(item => $"{item.Current.ControlType.ProgrammaticName} {item.Current.AutomationId} {item.Current.Name} {item.Current.BoundingRectangle}"));
            throw;
        }
    }

    internal static AutomationElement FindItem(AutomationElement root, string name) {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(10)) {
            var item = ApplicationUi.Find(root, "WebEditor.Files").FindAll(TreeScope.Descendants,
                new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem),
                    new PropertyCondition(AutomationElement.NameProperty, name))).Cast<AutomationElement>()
                .FirstOrDefault(element => !element.Current.IsOffscreen);
            if (item != null) return item;
            Thread.Sleep(100);
        }
        throw new TimeoutException("Web file tree item not found: " + name);
    }

    internal static Task WaitForEditorStatusAsync(AutomationElement root, string expected) => AppProcessSession.WaitAsync(() =>
        Task.Run(() => ApplicationUi.Find(root, "WebEditor.CodeSurface", visible: true).Current.ItemStatus == expected), "Monaco status " + expected);
}
