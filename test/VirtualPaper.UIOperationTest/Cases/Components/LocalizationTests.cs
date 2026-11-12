using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.IntelligentPanel.ViewModels;
using VirtualPaper.IntelligentPanel.Views.StyleTransferComp;
using VirtualPaper.Models.Cores;
using VirtualPaper.PlayerWeb.Core.WebView.Windows;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIOperationTest.Infrastructure;
using VirtualPaper.TestSupport;
using Workloads.Creation.StaticImg.Views.Tools;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class LocalizationTests {
    private static string[] Languages => [.. SupportedLanguages.Languages.Select(language => language.Codes[0]), "en-US"];
    private static string Expected(string language, string key) => LocalizedTestStrings.Read(AppContext.BaseDirectory, language, key);

    public static async Task RefreshAndPreserveStateAsync(UiTestContext context) {
        var regionalCulture = System.Globalization.CultureInfo.CurrentCulture;
        using var services = new ServiceCollection()
            .AddSingleton<StyleTransferAddTaskViewModel>()
            .AddSingleton<StyleTranferViewModel>()
            .BuildServiceProvider();
        AppServiceLocator.Services = services;
        var page = await context.OpenAsync<LocalizationTestPage>(new());
        try {
            var brush = new PaintBrushControl { BrushThickness = 37, BrushOpacity = 65 };
            page.TestSurface.Child = brush;
            await context.WaitAsync(() => brush.IsLoaded && brush.SelectedBrush != null);
            Descendants<Expander>(brush).Single().IsExpanded = true;
            await context.WaitForLayoutAsync();
            var selectedBrush = brush.SelectedBrush;
            var dropdown = Descendants<DropDownButton>(brush).Single();
            var label = (TextBlock)dropdown.Content;
            foreach (string language in Languages) {
                await LanguageUtil.SetLanguageAsync(language);
                await context.WaitAsync(() => label.Text == Expected(language, Constants.I18n.Project_StaticImg_Text_Paintbrush),
                    "Translate the selected brush");
                var flyout = (Flyout)dropdown.Flyout;
                flyout.ShowAt(dropdown);
                await context.WaitAsync(() => Descendants<TextBlock>((ListView)flyout.Content)
                    .Any(text => text.Text == label.Text), "Translate the brush flyout");
                flyout.Hide();
                Check(ReferenceEquals(selectedBrush, brush.SelectedBrush) && brush.BrushThickness == 37 && brush.BrushOpacity == 65,
                    "Language change reset brush selection or settings");
            }
            context.Evidence["BrushSelectionAndSettingsPreserved"] = true;

            var addTask = new StyleTransferAddTask();
            page.TestSurface.Child = addTask;
            await context.WaitAsync(() => addTask.IsLoaded);
            var viewModel = (StyleTransferAddTaskViewModel)addTask.DataContext;
            var options = ((IEnumerable<StyleOptionItem>)((GridView)addTask.FindName("styleGridView")).ItemsSource).ToArray();
            viewModel.SelectedStyle = options[0];
            var selectedStyle = viewModel.SelectedStyle;
            string source = Path.Combine(context.Root, "source.png");
            WallpaperLibraryTests.WriteImage(source);
            viewModel.SourceFilePath = source;
            viewModel.UpdateCardComponentUI();
            foreach (string language in Languages) {
                await LanguageUtil.SetLanguageAsync(language);
                await context.WaitAsync(() => Descendants<TextBlock>(addTask).Any(text => text.Name == "styleName" && text.Text == selectedStyle.Name),
                    "Translate the selected preset style");
                Check(options.All(style => style.Name == Expected(language, style.NameKey!)), "A preset name retained the previous language");
                Check(ReferenceEquals(selectedStyle, viewModel.SelectedStyle) && viewModel.IsNextEnable
                    && viewModel.SourceFilePath.EndsWith("source.png", StringComparison.Ordinal), "Language change reset task inputs");
                Check(viewModel.NextStepBtnText == Expected(language, Constants.I18n.Text_Confirm), "Task action retained the previous language");
            }
            // A missing name must use a live prompt without overwriting a real name.
            viewModel.SelectedStyle = new StyleOptionItem();
            foreach (string language in Languages) {
                await LanguageUtil.SetLanguageAsync(language);
                await context.WaitAsync(() => Descendants<TextBlock>(addTask).Any(text =>
                    text.Name == "styleNameFallback"
                    && text.Visibility == Visibility.Visible
                    && text.Text == Expected(language, Constants.I18n.Intelligent_Task_Style_Img_Import)), "Translate the unnamed style prompt");
            }
            context.Evidence["PresetNamesAndAddTaskFallbackUpdated"] = true;

            var queue = services.GetRequiredService<StyleTranferViewModel>();
            var preset = TaskItem("Anime snapshot", Constants.I18n.Intelligent_Style_Type_Anime);
            var custom = TaskItem("My custom style");
            var unnamed = TaskItem(null);
            queue.Tasks.Add(preset);
            queue.Tasks.Add(custom);
            queue.Tasks.Add(unnamed);
            var tasks = new StyleTransferTasksView { Tasks = queue.Tasks };
            bool taskPageUnloaded = false;
            addTask.Unloaded += (_, _) => taskPageUnloaded = true;
            string? detachedPresetName = options[0].Name;
            page.TestSurface.Child = tasks;
            await context.WaitAsync(() => taskPageUnloaded, "Unload the task page before switching language");
            await context.WaitAsync(() => Descendants<Expander>(tasks).Count() == 3);
            foreach (var expander in Descendants<Expander>(tasks)) expander.IsExpanded = true;
            foreach (string language in Languages) {
                await LanguageUtil.SetLanguageAsync(language);
                await context.WaitAsync(() => Descendants<TextBlock>(tasks).Any(text => text.Text == preset.StyleName)
                    && Descendants<TextBlock>(tasks).Any(text => text.Name == "styleNameFallback"
                        && text.Text == Expected(language, Constants.I18n.Intelligent_Task_Style_Img_Import)
                        && text.Visibility == Visibility.Visible), "Translate queued styles and the unnamed style prompt");
                Check(preset.StyleName == Expected(language, Constants.I18n.Intelligent_Style_Type_Anime), "Queued preset retained its old name");
                Check(custom.StyleName == "My custom style" && queue.Tasks.Count == 3, "Language change modified custom task data");
                Check(options[0].Name == detachedPresetName, "An unloaded task page retained its language event subscription");
            }
            context.Evidence["QueuedPresetAndFallbackUpdated"] = true;
            context.Evidence["CustomNamesPreserved"] = true;
            context.Evidence["UnloadedTaskStopsLanguageUpdates"] = true;

            string metadata = Path.Combine(context.Root, "metadata.json");
            var data = new WpBasicData { Title = "My wallpaper", FilePath = source };
            await JsonSaver.SaveAsync(metadata, data, WpBasicDataContext.Default);
            var startArgs = new StartArgsWeb { FilePath = source, RuntimeType = "RImage", WpBasicDataFilePath = metadata };
            var adjust = new AdjustConfig(JsonSerializer.Serialize(startArgs, StartArgsWebContext.Default.StartArgsWeb));
            var details = new OnlyDetails(DataConfigTab.GeneralInfo, data);
            try {
                Check(details.Title == LanguageUtil.GetI18n(Constants.I18n.PlayerWeb_DetailsWindowTitle), "Details title was not initialized");
                await context.WaitAsync(() => adjust.Title.StartsWith("My wallpaper", StringComparison.Ordinal), "Load wallpaper title");
                foreach (string language in Languages) {
                    await LanguageUtil.SetLanguageAsync(language);
                    Check(adjust.Title == $"My wallpaper ({Expected(language, Constants.I18n.Text_Adjust)})"
                        && adjust.ContentHost.Title == adjust.Title, "Adjust title was not translated or lost the wallpaper name");
                    Check(details.Title == Expected(language, Constants.I18n.PlayerWeb_DetailsWindowTitle)
                        && details.ContentHost.Title == Expected(language, Constants.I18n.Text_Details), "Details title was not translated");
                }
                context.Evidence["WindowTitlesUpdated"] = true;
                context.Evidence["CatalogLanguagesVerified"] = SupportedLanguages.Languages.Select(language => language.Codes[0]).ToArray();
                Check(System.Globalization.CultureInfo.CurrentCulture.Name == regionalCulture.Name,
                    "Changing UI language replaced regional date/number formatting");
            }
            finally {
                details.Close();
                adjust.Close();
            }
        }
        finally {
            page.TestSurface.Child = null;
            await LanguageUtil.SetLanguageAsync("en-US");
            AppServiceLocator.Services = null!;
        }
    }

    private static StyleTransferTaskItem TaskItem(string? name, string? key = null) => new(new StyleTransferData(
        "source.png", "1 KB", ".png", 256, 144, "style.png", name, "1 KB", ".png", key));

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
