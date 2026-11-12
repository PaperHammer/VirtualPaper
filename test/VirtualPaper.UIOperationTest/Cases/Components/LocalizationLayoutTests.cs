using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.IntelligentPanel.ViewModels;
using VirtualPaper.IntelligentPanel.Views.StyleTransferComp;
using VirtualPaper.IntelligentPanel.Views.SuperResolutionComp;
using VirtualPaper.UIComponent.Input;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIOperationTest.Infrastructure;
using Windows.Foundation;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class LocalizationLayoutTests {
    public static async Task RunAsync(UiTestContext context) {
        var previousServices = AppServiceLocator.Services;
        using var services = new ServiceCollection().AddTransient<StyleTransferAddTaskViewModel>()
            .AddTransient<SuperResolutionAddTaskViewModel>().BuildServiceProvider();
        AppServiceLocator.Services = services;
        LocalizationTestPage? page = null;
        var samples = new List<object>();
        try {
            page = await context.OpenAsync<LocalizationTestPage>(new());
            foreach (string locale in new[] { "de-DE", "fr-FR", "ru-RU" }) {
                await LanguageUtil.SetLanguageAsync(locale);
                foreach (var theme in new[] { ElementTheme.Light, ElementTheme.Dark }) {
                    page.RequestedTheme = theme;
                    foreach (int width in new[] { 1440, 800 }) {
                        await context.ResizeAsync(width, 860);
                        foreach (var introduction in new UserControl[] { new StyleTransferIntroduction(), new SuperResolutionIntroduction() }) {
                            page.TestSurface.Child = introduction;
                            await context.WaitAsync(() => introduction.IsLoaded && introduction.ActualWidth > 0);
                            await context.WaitForLayoutAsync();
                            var preview = Descendants<Viewbox>(introduction).Single();
                            var description = (FrameworkElement)introduction.FindName("IntroductionText");
                            var imageBounds = Bounds(preview, page);
                            var textBounds = Bounds(description, page);
                            Check(Math.Abs(imageBounds.Width / imageBounds.Height - 16d / 9) < .02,
                                "Introduction preview lost its aspect ratio");
                            bool wide = introduction.ActualWidth >= 960;
                            Check(wide ? textBounds.Left >= imageBounds.Right - 1 : textBounds.Top >= imageBounds.Bottom - 1,
                                "Introduction did not switch between horizontal and vertical layout");
                            CheckHorizontal(description, page);
                            Check(Descendants<TextBlock>(description).All(text => !text.IsTextTrimmed), "Introduction text was clipped");
                        }
                        foreach (var form in new Page[] { new StyleTransferAddTask(), new SuperResolutionAddTask() }) {
                            var scroll = new ScrollViewer { Content = form, HorizontalScrollMode = ScrollMode.Disabled,
                                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalContentAlignment = HorizontalAlignment.Stretch };
                            page.TestSurface.Child = scroll;
                            await context.WaitAsync(() => form.IsLoaded && form.ActualWidth > 0);
                            await context.WaitForLayoutAsync();
                            var source = Descendants<Button>(form).Single(button =>
                                Microsoft.UI.Xaml.Automation.AutomationProperties.GetAutomationId(button).EndsWith(".Source", StringComparison.Ordinal));
                            var options = (FrameworkElement)form.FindName("OptionsPanel");
                            var sourceBounds = Bounds(source, page);
                            var optionBounds = Bounds(options, page);
                            Check(width >= 1100 ? optionBounds.Left >= sourceBounds.Right - 1 : optionBounds.Top >= sourceBounds.Bottom - 1,
                                "Task form did not switch between horizontal and stacked layout");
                            CheckHorizontal(source, page);
                            CheckHorizontal(options, page);
                        }
                        // Completed visual fixtures exercise the longest preservation label and all four actions.
                        string path = @"C:\Pictures\A very long wallpaper collection folder\Another long directory\source-wallpaper.png";
                        var style = new StyleTransferTaskItem(new StyleTransferData(path, "1 KB", ".png", 256, 144,
                            "style.png", "My custom style", "1 KB", ".png")) { Status = System.Threading.Tasks.TaskStatus.RanToCompletion };
                        var enhance = new SuperResolutionTaskItem(new SuperResolutionData(path, "1 KB", ".png", 256, 144,
                            EnhanceMode.SuperResolution, 4, 1024, 576)) { Status = System.Threading.Tasks.TaskStatus.RanToCompletion };
                        foreach (var tasks in new UserControl[] {
                            new StyleTransferTasksView { Tasks = new ObservableCollection<StyleTransferTaskItem> { style } },
                            new SuperResolutionTasksView { Tasks = new ObservableCollection<SuperResolutionTaskItem> { enhance } }
                        }) {
                            page.TestSurface.Child = tasks;
                            await context.WaitAsync(() => tasks.IsLoaded && Descendants<Expander>(tasks).Any());
                            await context.WaitForLayoutAsync();
                            var actions = Descendants<ArcButton>(tasks).Take(4).ToArray();
                            Check(actions.Length == 4, "Task actions are missing");
                            var bounds = actions.Select(button => Bounds(button, page)).ToArray();
                            Check(bounds.Max(rect => rect.Top) - bounds.Min(rect => rect.Top) < 2, "Task actions wrapped onto another row");
                            foreach (var action in actions) {
                                CheckHorizontal(action, page);
                                Check(action.ActualWidth >= 32 && action.ActualHeight >= 32, "Task action hit area is too small");
                                Check(Descendants<TextBlock>(action).All(text => !text.IsTextTrimmed), "Task action text was clipped");
                            }
                        }
                        samples.Add(new { Locale = locale, Theme = theme.ToString(), Width = width });
                    }
                }
            }
            context.Evidence["LayoutSamples"] = samples;
            context.Evidence["IntroductionAspectRatioAndLayoutVerified"] = true;
            context.Evidence["TaskFormsAndSingleRowActionsVerified"] = true;
        }
        catch {
            context.SaveFailureSnapshot("localized-layout");
            throw;
        }
        finally {
            if (page != null) page.TestSurface.Child = null;
            try { await LanguageUtil.SetLanguageAsync("en-US"); }
            finally { AppServiceLocator.Services = previousServices; }
        }
    }

    private static Rect Bounds(FrameworkElement control, FrameworkElement root) =>
        control.TransformToVisual(root).TransformBounds(new Rect(0, 0, control.ActualWidth, control.ActualHeight));

    private static void CheckHorizontal(FrameworkElement control, FrameworkElement root) {
        var bounds = Bounds(control, root);
        Check(bounds.Width > 0 && bounds.Left >= -1 && bounds.Right <= root.ActualWidth + 1,
            "Localized control extends beyond the viewport: " + control.GetType().Name);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
