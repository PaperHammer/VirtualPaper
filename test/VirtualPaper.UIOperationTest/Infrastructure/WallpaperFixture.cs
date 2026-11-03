using System.Diagnostics;
using System.Text.Json;
using System.Windows.Automation;
using Google.Protobuf.WellKnownTypes;
using VirtualPaper.Common;
using VirtualPaper.Models.Cores;

namespace VirtualPaper.UIOperationTest.Infrastructure;

internal sealed record WallpaperFixture(Process Ui, AutomationElement Root, WpBasicData Data) {
    public static async Task<WallpaperFixture> ImportAsync(AppProcessSession session, string source) {
        var ui = await session.OpenUiAsync();
        var root = await Task.Run(() => {
            var window = AutomationElement.FromHandle(ui.MainWindowHandle);
            ApplicationUi.Select(ApplicationUi.Find(window, "App.Navigation.Library"));
            ApplicationUi.Invoke(ApplicationUi.Find(window, "Library.Import"));
            ApplicationUi.Invoke(ApplicationUi.Find(window, "Library.Import.Files"));
            NativeFileDialog.OpenFile(ui, ui.MainWindowHandle, source);
            return window;
        });
        string library = (await session.Settings.GetSettingsAsync(new Empty())).WallpaperDir;
        WpBasicData? data = null;
        await AppProcessSession.WaitAsync(async () => {
            var files = Directory.GetFiles(library, Constants.Field.WpBasicDataFileName, SearchOption.AllDirectories);
            if (files.Length != 1) return false;
            try {
                data = JsonSerializer.Deserialize(await AppProcessSession.ReadTextSharedAsync(files[0]), WpBasicDataContext.Default.WpBasicData);
                return data != null && File.Exists(data.ThumbnailPath);
            }
            catch (IOException) { return false; }
            catch (JsonException) { return false; }
        }, "Imported fixture metadata and thumbnail");
        return new(ui, root, data!);
    }

    public async Task ApplyAsync(bool depth = false) {
        await Task.Run(() => {
            Cases.WallpaperLibraryTests.OpenItemMenu(Ui.MainWindowHandle, Root, Data.WallpaperUid, "Library.Item.Apply");
            if (depth) {
                var choices = ApplicationUi.Find(Root, "Wallpaper.CreateType").FindAll(TreeScope.Children,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
                UiTestContext.Check(choices.Count == 2, "Expected ordinary image and 3D image choices");
                ApplicationUi.Select(choices[1]);
            }
            ApplicationUi.Invoke(ApplicationUi.Find(Root, "PrimaryButton", visible: true));
        });
        await AppProcessSession.WaitForJsonAsync(Constants.CommonPaths.WallpaperLayoutPath,
            json => json.AsArray().Count == 1 && json[0]?["FolderPath"]?.GetValue<string>() == Data.FolderPath,
            depth ? TimeSpan.FromMinutes(2) : null);
    }
}
