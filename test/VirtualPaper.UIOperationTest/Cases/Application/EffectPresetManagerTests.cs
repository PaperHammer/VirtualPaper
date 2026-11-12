using System.Windows.Automation;
using VirtualPaper.Common;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class EffectPresetManagerTests {
    public static async Task RunAsync(UiTestContext context) {
        var session = new AppProcessSession();
        string path = Path.Combine(Constants.CommonPaths.AppDataDir, "effect_presets.json");
        byte[]? previous = File.Exists(path) ? await File.ReadAllBytesAsync(path) : null;
        try {
            // Other cases share the isolated host profile; restore their catalog afterward.
            File.Delete(path);
            await RunManagerAsync(context, session);
        }
        finally {
            try { await session.DisposeAsync(); }
            finally {
                if (previous == null) File.Delete(path);
                else await File.WriteAllBytesAsync(path, previous);
            }
        }
    }

    private static async Task RunManagerAsync(UiTestContext context, AppProcessSession session) {
        await session.StartAsync();
        var ui = await session.OpenUiAsync();
        var root = await Task.Run(() => Open(ui.MainWindowHandle));
        var store = new EffectPresetStore();
        var builtIns = (await store.LoadAsync()).Where(item => item.IsBuiltIn).ToArray();
        Check(builtIns.Length > 0, "Preset catalog has no built-in templates");
        await Task.Run(() => {
            Check(!ApplicationUi.Find(root, "EffectPresets.Delete", enabled: false).Current.IsEnabled, "Built-in preset can be deleted");
            Check(!ApplicationUi.Find(root, "PrimaryButton", visible: true, enabled: false).Current.IsEnabled, "Built-in preset can be saved");
            Check(!ApplicationUi.Find(root, "EffectPresets.Type", enabled: false).Current.IsEnabled, "Built-in type is editable");
            var name = ApplicationUi.Find(root, "EffectPresets.Name.ReadOnly", visible: true);
            Check(!string.IsNullOrWhiteSpace(name.Current.Name), "Built-in name is empty");
            Check(HasVisibleState(root), "Built-in read-only notice is missing");
            ApplicationUi.Invoke(ApplicationUi.Find(root, "EffectPresets.New"));
            Check(SelectedCount(root) == 0, "New draft left a saved preset selected");
            Check(!HasVisibleState(root), "Untouched draft was marked modified");
            ApplicationUi.SetText(ApplicationUi.Find(root, "EffectPresets.Name"), "UI acceptance original");
        });
        await WaitStateAsync(root, true);
        await Task.Run(() => ApplicationUi.Invoke(ApplicationUi.Find(root, "PrimaryButton", visible: true)));
        EffectPreset? original = null;
        await AppProcessSession.WaitAsync(async () => {
            original = (await store.LoadAsync()).SingleOrDefault(item => item.Name == "UI acceptance original");
            return original != null;
        }, "Preset created by manager Save");
        Check(original!.RuntimeType == RuntimeType.RImage, "New preset did not use the image defaults");
        Check(EffectPresets.HaveSameParameters(original, EffectPresets.Capture("defaults", RuntimeType.RImage, EffectPresets.Defaults(RuntimeType.RImage))), "New preset changed default parameters");
        await WaitStateAsync(root, false);
        await Task.Run(() => {
            var rows = Rows(root);
            ApplicationUi.Select(rows[0]);
            ApplicationUi.Select(FindRow(root, original.Name));
        });
        await WaitStateAsync(root, false);
        await Task.Run(() => {
            ApplicationUi.Invoke(ApplicationUi.Find(root, "EffectPresets.NewFromSelected"));
            Check(SelectedCount(root) == 0, "Copied draft left a saved preset selected");
            Check(!HasVisibleState(root), "Untouched copy was marked modified");
            ApplicationUi.SetText(ApplicationUi.Find(root, "EffectPresets.Name"), "UI acceptance copy");
        });
        await WaitStateAsync(root, true);
        await Task.Run(() => ApplicationUi.Invoke(ApplicationUi.Find(root, "PrimaryButton", visible: true)));
        EffectPreset? copy = null;
        await AppProcessSession.WaitAsync(async () => {
            copy = (await store.LoadAsync()).SingleOrDefault(item => item.Name == "UI acceptance copy");
            return copy != null;
        }, "Copied preset saved");
        Check(copy!.Id != original.Id && EffectPresets.HaveSameParameters(original, copy), "Copy lost parameters or reused the original identity");
        await WaitStateAsync(root, false);
        string path = Path.Combine(Constants.CommonPaths.AppDataDir, "effect_presets.json");
        byte[] beforeCancel = await File.ReadAllBytesAsync(path);
        await Task.Run(() => {
            ApplicationUi.Invoke(ApplicationUi.Find(root, "EffectPresets.Delete"));
            Check(ApplicationUi.Find(root, "EffectPresets.Delete.Name", visible: true).Current.Name == copy.Name, "Delete confirmation targets a different preset");
            ApplicationUi.Invoke(ApplicationUi.Find(root, "EffectPresets.Delete.Cancel", visible: true));
            ApplicationUi.WaitUntilGone(root, "EffectPresets.Delete.Confirm");
        });
        Check(beforeCancel.SequenceEqual(await File.ReadAllBytesAsync(path)), "Cancel delete changed the preset catalog");
        await Task.Run(() => {
            ApplicationUi.Invoke(ApplicationUi.Find(root, "EffectPresets.Delete"));
            ApplicationUi.Invoke(ApplicationUi.Find(root, "EffectPresets.Delete.Confirm", visible: true));
        });
        await AppProcessSession.WaitAsync(async () => {
            var users = (await store.LoadAsync()).Where(item => !item.IsBuiltIn).ToArray();
            return users.Length == 1 && users[0].Id == original.Id;
        }, "Delete only the confirmed preset");
        await WaitStateAsync(root, false);
        await Task.Run(() => ApplicationUi.SetText(ApplicationUi.Find(root, "EffectPresets.Name"), "Unsaved name must disappear"));
        await WaitStateAsync(root, true);
        await Task.Run(() => {
            ApplicationUi.Invoke(ApplicationUi.Find(root, "CloseButton", visible: true));
            ApplicationUi.WaitUntilGone(root, "EffectPresets.List");
        });
        var usersAfterClose = (await store.LoadAsync()).Where(item => !item.IsBuiltIn).ToArray();
        Check(usersAfterClose.Length == 1 && usersAfterClose[0].Name == original.Name, "Closing silently saved the dirty draft");
        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        root = await Task.Run(() => Open(ui.MainWindowHandle));
        await WaitStateAsync(root, false);
        await Task.Run(() => {
            Check(Rows(root).Count == builtIns.Length + 1, "Reopening lost a saved preset or kept a deleted one");
            Check(((ValuePattern)ApplicationUi.Find(root, "EffectPresets.Name").GetCurrentPattern(ValuePattern.Pattern)).Current.Value == original.Name, "Reopening did not load the saved name");
            ApplicationUi.Invoke(ApplicationUi.Find(root, "CloseButton", visible: true));
            ApplicationUi.WaitUntilGone(root, "EffectPresets.List");
        });
        var finalBuiltIns = (await store.LoadAsync()).Where(item => item.IsBuiltIn).ToArray();
        Check(builtIns.Length == finalBuiltIns.Length && builtIns.Zip(finalBuiltIns).All(pair => pair.First.Id == pair.Second.Id
            && EffectPresets.HaveSameParameters(pair.First, pair.Second)), "Managing user presets changed built-in templates");
        await session.StopAsync();
        session.RecordEvidence(context);
        context.Evidence["BuiltInReadOnlyCreateCopySaveDeleteCancelDirtyCloseAndReopenVerified"] = true;
    }

    private static AutomationElement Open(nint window) {
        var root = AutomationElement.FromHandle(window);
        ApplicationUi.Select(ApplicationUi.Find(root, "App.Navigation.Library"));
        ApplicationUi.Invoke(ApplicationUi.Find(root, "Library.More"));
        ApplicationUi.Invoke(ApplicationUi.Find(root, "Library.EffectPresets", visible: true));
        ApplicationUi.Find(root, "EffectPresets.New");
        return root;
    }
    private static List<AutomationElement> Rows(AutomationElement root) => ApplicationUi.Find(root, "EffectPresets.List")
        .FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)).Cast<AutomationElement>().ToList();
    private static AutomationElement FindRow(AutomationElement root, string name) => Rows(root).Single(row => row.Current.Name == name
        || row.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name)) != null);
    private static int SelectedCount(AutomationElement root) => ((SelectionPattern)ApplicationUi.Find(root, "EffectPresets.List").GetCurrentPattern(SelectionPattern.Pattern)).Current.GetSelection().Length;
    private static bool HasVisibleState(AutomationElement root) => root.FindAll(TreeScope.Descendants,
        new PropertyCondition(AutomationElement.AutomationIdProperty, "EffectPresets.State")).Cast<AutomationElement>()
        .Any(item => !item.Current.IsOffscreen && !string.IsNullOrEmpty(item.Current.Name));
    private static Task WaitStateAsync(AutomationElement root, bool visible) => AppProcessSession.WaitAsync(() => Task.Run(() =>
        HasVisibleState(root) == visible && ApplicationUi.Find(root, "EffectPresets.New", enabled: false).Current.IsEnabled), "Preset modified state " + visible);
}
