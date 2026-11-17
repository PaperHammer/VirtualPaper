using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using VirtualPaper.AppSettingsPanel.Utils;
using VirtualPaper.Common.Logging;
using VirtualPaper.UIComponent.Dialogs;
using VirtualPaper.UIComponent.Utils;
using Windows.Storage;
using Windows.System;

namespace VirtualPaper.AppSettingsPanel.Views;

internal static class LegalDocumentDialog {
    private static readonly Regex Links = new(@"\[([^\]]+)\]\(([^)\s]+)\)", RegexOptions.Compiled);

    internal static async Task ShowAsync(FrameworkElement owner, string title, string section) {
        var body = await CreateContentAsync(section);
        var dialog = new ArcContentDialog {
            Title = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap },
            Content = body, CloseButtonText = LanguageUtil.GetI18n("Text_Close"),
            DialogWidth = 800, DialogHeight = 680,
            XamlRoot = owner.XamlRoot, RequestedTheme = owner.ActualTheme
        };
        AutomationProperties.SetAutomationId(dialog, "Settings.Legal.Dialog");
        await dialog.ShowAsync();
    }

    internal static async Task<StackPanel> CreateContentAsync(string section) {
        string root = Path.Combine(AppContext.BaseDirectory, "Legal");
        string document = LegalDocuments.GetPath(root, LanguageUtil.CurrentLanguage);
        string text = await LegalDocuments.ReadSectionAsync(root, LanguageUtil.CurrentLanguage, section);
        var body = new StackPanel { Spacing = 12 };
        int paragraphIndex = 0;
        // These bundled documents use headings and paragraphs; no browser or script engine is needed.
        foreach (string raw in text.Replace("\r\n", "\n").Split('\n')) {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith('#')) {
                int level = line.StartsWith("### ") ? 3 : line.StartsWith("## ") ? 2 : 1;
                body.Children.Add(new TextBlock {
                    Text = line.TrimStart('#', ' '), TextWrapping = TextWrapping.Wrap,
                    FontSize = level == 1 ? 22 : level == 2 ? 19 : 16,
                    FontWeight = FontWeights.SemiBold, IsTextSelectionEnabled = true,
                    Margin = new Thickness(0, level == 1 ? 0 : 8, 0, 0)
                });
                continue;
            }
            var paragraph = new Paragraph();
            int previous = 0;
            foreach (Match match in Links.Matches(line)) {
                paragraph.Inlines.Add(new Run { Text = Plain(line[previous..match.Index]) });
                string target = match.Groups[2].Value;
                var link = new Hyperlink();
                link.Inlines.Add(new Run { Text = Plain(match.Groups[1].Value) });
                link.Click += async (_, _) => {
                    try {
                        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                            await Launcher.LaunchUriAsync(uri);
                        else if (LegalDocuments.ResolveLocalLink(root, document, target) is { } path)
                            await Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(path));
                    }
                    catch (Exception ex) { ArcLog.GetLogger(typeof(LegalDocumentDialog)).Error(ex); }
                };
                paragraph.Inlines.Add(link);
                previous = match.Index + match.Length;
            }
            paragraph.Inlines.Add(new Run { Text = Plain(line[previous..]) });
            var block = new RichTextBlock { FontSize = 14, LineHeight = 23, IsTextSelectionEnabled = true };
            AutomationProperties.SetAutomationId(block, $"Settings.Legal.Paragraph.{paragraphIndex++}");
            block.Blocks.Add(paragraph);
            body.Children.Add(block);
        }
        return body;
    }

    private static string Plain(string text) => text.Replace("`", string.Empty);
}
