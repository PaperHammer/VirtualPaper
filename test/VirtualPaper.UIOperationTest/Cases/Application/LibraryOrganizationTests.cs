using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Automation;
using Google.Protobuf.WellKnownTypes;
using VirtualPaper.Common;
using VirtualPaper.Models.Cores;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class LibraryOrganizationTests {
    public static async Task RunAsync(UiTestContext context) {
        string firstSource = Path.Combine(context.Root, "organization-first.png");
        string source = Path.Combine(context.Root, "organization-second.png");
        WallpaperLibraryTests.WriteImage(firstSource);
        WallpaperLibraryTests.WriteImage(source, 192, 128);
        byte[] sourceHash = SHA256.HashData(await File.ReadAllBytesAsync(source));
        await using var session = new AppProcessSession();
        await session.StartAsync();
        var first = await WallpaperFixture.ImportAsync(session, firstSource);
        var ui = first.Ui;
        var root = first.Root;
        await Task.Run(() => {
            ApplicationUi.Invoke(ApplicationUi.Find(root, "Library.Import"));
            ApplicationUi.Invoke(ApplicationUi.Find(root, "Library.Import.Files"));
            NativeFileDialog.OpenFile(ui, ui.MainWindowHandle, source);
        });
        string library = (await session.Settings.GetSettingsAsync(new Empty())).WallpaperDir;
        WpBasicData? data = null;
        await AppProcessSession.WaitAsync(() => {
            try {
                data = Directory.GetFiles(library, Constants.Field.WpBasicDataFileName, SearchOption.AllDirectories)
                    .Select(WpBasicData.Load).SingleOrDefault(item => item.WallpaperUid != first.Data.WallpaperUid);
                return Task.FromResult(data != null && File.Exists(data.ThumbnailPath));
            }
            catch (IOException) { return Task.FromResult(false); }
            catch (JsonException) { return Task.FromResult(false); }
        }, "Second organization fixture import");
        string uid = data!.WallpaperUid;
        string metadata = Path.Combine(data.FolderPath, Constants.Field.WpBasicDataFileName);
        string organization = Path.Combine(Constants.CommonPaths.AppDataDir, "wallpaper_library.json");
        string[] ids = [first.Data.WallpaperUid, uid];
        await WallpaperLibraryTests.WaitForItemsAsync(root, 2);
        string[] beforeOrder = await Task.Run(() => Order(root, ids));
        Check(beforeOrder.Length == 2 && beforeOrder[0] == "Library.Item." + uid, "Expected both fixtures with the newest card first");
        await Task.Run(() => WallpaperLibraryTests.OpenItemMenu(ui.MainWindowHandle, root, uid, "Library.Item.AddFavorite"));
        await AppProcessSession.WaitForJsonAsync(organization, json => json["Items"]?[uid]?["Favorite"]?.GetValue<bool>() == true);
        await WaitFavoriteAsync(root, uid, true);

        await Task.Run(() => {
            WallpaperLibraryTests.OpenItemMenu(ui.MainWindowHandle, root, uid, "Library.Item.EditInfo");
            ApplicationUi.SetText(ApplicationUi.Find(root, "Library.Edit.Title"), "Organized wallpaper");
            ApplicationUi.SetText(ApplicationUi.Find(root, "Library.Edit.Description"), "Persisted by the real information editor.");
            ApplicationUi.CommitToken(ui.MainWindowHandle, ApplicationUi.Find(root, "Library.Edit.Authors"), "Organization author");
        });
        await WaitEmptyInputAsync(root, "Library.Edit.Authors");
        await Task.Run(() => ApplicationUi.CommitToken(ui.MainWindowHandle, ApplicationUi.Find(root, "Library.Edit.Tags"), "organization-tag"));
        await WaitEmptyInputAsync(root, "Library.Edit.Tags");
        await Task.Run(() => {
            ApplicationUi.SetText(ApplicationUi.Find(root, "Library.Edit.Tags"), "pending-tag");
            ApplicationUi.Invoke(ApplicationUi.Find(root, "PrimaryButton", visible: true));
            ApplicationUi.WaitUntilGone(root, "Library.Edit.Title");
        });
        await AppProcessSession.WaitForJsonAsync(metadata, json => json["Title"]?.GetValue<string>() == "Organized wallpaper"
            && json["Desc"]?.GetValue<string>() == "Persisted by the real information editor."
            && json["Authors"]?.GetValue<string>()?.Contains("Organization author", StringComparison.Ordinal) == true);
        await AppProcessSession.WaitForJsonAsync(organization, json => json["Items"]?[uid]?["Tags"] is { } tags
            && tags.AsArray().Select(tag => tag!.GetValue<string>()).Order().SequenceEqual(new[] { "organization-tag", "pending-tag" }));
        Check(beforeOrder.SequenceEqual(await Task.Run(() => Order(root, ids))), "Editing information changed the card's default position");

        await Task.Run(() => {
            ApplicationUi.Expand(ApplicationUi.Find(root, "Library.Filters"));
            ((TogglePattern)ApplicationUi.Find(root, "Library.FavoritesOnly", visible: true).GetCurrentPattern(TogglePattern.Pattern)).Toggle();
            CollapseFilters(root);
        });
        await WallpaperLibraryTests.WaitForItemsAsync(root, 1);
        await Task.Run(() => WallpaperLibraryTests.FindItem(root, uid));
        await FilterTagAsync(root, "organization-ta", 1);
        await FilterTagAsync(root, "no-matching-personal-tag", 0);
        await FilterTagAsync(root, "", 1);
        await Task.Run(() => {
            ApplicationUi.Expand(ApplicationUi.Find(root, "Library.Filters"));
            ((TogglePattern)ApplicationUi.Find(root, "Library.FavoritesOnly", visible: true).GetCurrentPattern(TogglePattern.Pattern)).Toggle();
            CollapseFilters(root);
            ApplicationUi.SetText(ApplicationUi.Find(root, "Library.Search"), "Organized wallpaper");
        });
        await WallpaperLibraryTests.WaitForItemsAsync(root, 1);
        await Task.Run(() => ApplicationUi.SetText(ApplicationUi.Find(root, "Library.Search"), ""));
        await WallpaperLibraryTests.WaitForItemsAsync(root, 2);

        await session.CloseUiAsync();
        ui = await session.OpenUiAsync();
        root = await Task.Run(() => {
            var window = AutomationElement.FromHandle(ui.MainWindowHandle);
            ApplicationUi.Select(ApplicationUi.Find(window, "App.Navigation.Library"));
            return window;
        });
        await WallpaperLibraryTests.WaitForItemsAsync(root, 2);
        await WaitFavoriteAsync(root, uid, true);
        Check(beforeOrder.SequenceEqual(await Task.Run(() => Order(root, ids))), "Reopening lost the library order");
        await FilterTagAsync(root, "organization-ta", 1);
        await Task.Run(() => WallpaperLibraryTests.FindItem(root, uid));
        await FilterTagAsync(root, "", 2);
        await Task.Run(() => {
            WallpaperLibraryTests.OpenItemMenu(ui.MainWindowHandle, root, uid, "Library.Item.EditInfo");
            Check(Text(ApplicationUi.Find(root, "Library.Edit.Title")) == "Organized wallpaper", "Reopening lost the title");
            Check(Text(ApplicationUi.Find(root, "Library.Edit.Description")) == "Persisted by the real information editor.", "Reopening lost the description");
            ApplicationUi.Invoke(ApplicationUi.Find(root, "CloseButton", visible: true));
            ApplicationUi.WaitUntilGone(root, "Library.Edit.Title");
            WallpaperLibraryTests.OpenItemMenu(ui.MainWindowHandle, root, uid, "Library.Item.RemoveFavorite");
        });
        await AppProcessSession.WaitForJsonAsync(organization, json => json["Items"]?[uid]?["Favorite"]?.GetValue<bool>() == false);
        await WaitFavoriteAsync(root, uid, false);
        Check(sourceHash.SequenceEqual(SHA256.HashData(await File.ReadAllBytesAsync(source))), "Organization modified the input image");
        Check(sourceHash.SequenceEqual(SHA256.HashData(await File.ReadAllBytesAsync(WpBasicData.Load(metadata).FilePath))), "Organization modified imported media");
        await session.StopAsync();
        session.RecordEvidence(context);
        context.Evidence["FavoritesTagsInformationFiltersAndReopenVerified"] = true;
    }

    private static string Text(AutomationElement input) => ((ValuePattern)input.GetCurrentPattern(ValuePattern.Pattern)).Current.Value;
    private static Task WaitEmptyInputAsync(AutomationElement root, string id) => AppProcessSession.WaitAsync(() => Task.Run(() => Text(ApplicationUi.Find(root, id)) == ""), "Enter commits " + id);
    private static Task WaitFavoriteAsync(AutomationElement root, string uid, bool favorite) => AppProcessSession.WaitAsync(() => Task.Run(() => {
        var card = WallpaperLibraryTests.FindItem(root, uid);
        var button = ApplicationUi.Find(card, "Library.Card.Favorite", enabled: false);
        return ((TogglePattern)button.GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState == (favorite ? ToggleState.On : ToggleState.Off);
    }), "Card favorite state " + favorite);
    private static string[] Order(AutomationElement root, string[] ids) => ApplicationUi.Find(root, "Library.Items")
        .FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem))
        .Cast<AutomationElement>().Select(item => item.Current.AutomationId).Where(id => ids.Any(uid => id == "Library.Item." + uid)).ToArray();
    private static void CollapseFilters(AutomationElement root) => ((ExpandCollapsePattern)ApplicationUi.Find(root, "Library.Filters").GetCurrentPattern(ExpandCollapsePattern.Pattern)).Collapse();
    private static async Task FilterTagAsync(AutomationElement root, string text, int count) {
        await Task.Run(() => {
            ApplicationUi.Expand(ApplicationUi.Find(root, "Library.Filters"));
            ApplicationUi.SetText(ApplicationUi.Find(root, "Library.TagFilter", visible: true), text);
            CollapseFilters(root);
        });
        await WallpaperLibraryTests.WaitForItemsAsync(root, count);
    }
}
