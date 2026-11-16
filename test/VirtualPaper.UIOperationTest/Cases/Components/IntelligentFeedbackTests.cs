using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.IntelligentPanel.ViewModels;
using VirtualPaper.IntelligentPanel.Views.StyleTransferComp;
using VirtualPaper.IntelligentPanel.Views.SuperResolutionComp;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIOperationTest.Infrastructure;
using static VirtualPaper.UIOperationTest.Infrastructure.UiTestContext;

namespace VirtualPaper.UIOperationTest.Cases;

internal static class IntelligentFeedbackTests {
    public static async Task RunAsync(UiTestContext context) {
        var page = await context.OpenAsync<LocalizationTestPage>(new());
        using var super = new SuperResolutionViewModel();
        using var style = new StyleTranferViewModel();
        string source = Path.Combine(context.Root, "source.png");
        WallpaperLibraryTests.WriteImage(source, 8, 8);
        string reason = "Unable to process the selected image. " + string.Concat(Enumerable.Repeat("Detailed failure information. ", 25));
        SuperResolutionTaskItem SuperItem(TaskStatus status) => new(new SuperResolutionData(source, "1 KB", ".png", 8, 8,
            EnhanceMode.SuperResolution, 2, 16, 16)) { Status = status };
        StyleTransferTaskItem StyleItem(TaskStatus status) => new(new StyleTransferData(source, "1 KB", ".png", 8, 8,
            source, "Custom style", "1 KB", ".png")) { Status = status };
        var superFailed = SuperItem(TaskStatus.Faulted);
        var styleFailed = StyleItem(TaskStatus.Faulted);
        superFailed.SetFailureReason(new IOException(reason));
        styleFailed.SetFailureReason(new IOException(reason));
        super.Tasks.Add(superFailed);
        super.Tasks.Add(SuperItem(TaskStatus.Canceled));
        style.Tasks.Add(styleFailed);
        style.Tasks.Add(StyleItem(TaskStatus.Canceled));
        try {
            foreach (string locale in new[] { "en-US", "ru-RU" }) {
                await LanguageUtil.SetLanguageAsync(locale);
                foreach (var theme in new[] { ElementTheme.Light, ElementTheme.Dark }) {
                    page.RequestedTheme = theme;
                    foreach (var (view, failed) in new (UserControl, IntelligentTaskItem)[] {
                        (new SuperResolutionTasksView { Tasks = super.Tasks }, superFailed),
                        (new StyleTransferTasksView { Tasks = style.Tasks }, styleFailed)
                    }) {
                        page.ShowContent(view);
                        await context.WaitAsync(() => view.IsLoaded && FeedbackTestWindow.Descendants<Expander>(view).Count() == 2,
                            "Render failed and canceled task rows");
                        var reasonBlocks = FeedbackTestWindow.Descendants<TextBlock>(view).Where(text =>
                            AutomationProperties.GetAutomationId(text) == "Intelligent.Task.FailureReason").ToArray();
                        Check(reasonBlocks.Count(text => text.Visibility == Visibility.Visible) == 1,
                            "Canceled tasks displayed a failure reason or the failed task lost its reason");
                        var brief = reasonBlocks.Single(text => text.Visibility == Visibility.Visible);
                        Check(brief.Text == reason && brief.MaxLines == 2, "Failure summary is incomplete or unbounded");
                        Check(FeedbackTestWindow.Descendants<TextBlock>(view).Any(text =>
                            AutomationProperties.GetAutomationId(text) == "Intelligent.Task.Status"
                            && text.Text == LanguageUtil.GetI18n(Constants.I18n.Text_Task_Status_Failed)),
                            "Task failure status did not follow the selected language");
                        var expander = FeedbackTestWindow.Descendants<Expander>(view).Single(item => ReferenceEquals(item.DataContext, failed));
                        expander.IsExpanded = true;
                        await context.WaitForLayoutAsync();
                        var full = FeedbackTestWindow.Descendants<TextBlock>(expander).Single(text =>
                            text.Text == reason && text.IsTextSelectionEnabled);
                        Check(full.Text == reason && full.MaxLines == 0 && !full.IsTextTrimmed,
                            "Expanded failure details were truncated or could not be selected");
                    }
                }
            }
            // Verify removal/rebinding, rather than assuming a clean data template.
            var final = new SuperResolutionTasksView { Tasks = super.Tasks };
            page.ShowContent(final);
            await context.WaitAsync(() => FeedbackTestWindow.Descendants<Expander>(final).Count() == 2);
            super.Tasks.Remove(superFailed);
            super.Tasks.Add(SuperItem(TaskStatus.RanToCompletion));
            await context.WaitAsync(() => !FeedbackTestWindow.Descendants<TextBlock>(final).Any(text =>
                AutomationProperties.GetAutomationId(text) == "Intelligent.Task.FailureReason" && text.Visibility == Visibility.Visible),
                "New/canceled rows must not retain a removed task's failure");
            context.Evidence["FailureDetailsPreservedAcrossLanguagesAndThemes"] = true;
            context.Evidence["CanceledAndNewRowsDoNotShowOldFailures"] = true;
            context.Evidence["FixtureKind"] = "Production task controls and language-refresh owners with controlled task states";
        }
        catch {
            context.SaveFailureSnapshot("intelligent-feedback-failure");
            throw;
        }
        finally {
            await LanguageUtil.SetLanguageAsync("en-US");
            page.ShowContent(null);
            var tasks = super.Tasks.Cast<IntelligentTaskItem>().Concat(style.Tasks).Append(superFailed).Distinct().ToArray();
            super.Dispose();
            style.Dispose();
            foreach (var task in tasks) task.Cts.Dispose();
        }
    }
}
