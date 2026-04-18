using System;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace VirtualPaper.UIComponent.Utils {
    public static class SimpleMarkdownRenderer {
        public static void Render(string? markdown, RichTextBlock targetTextBlock) {
            targetTextBlock.Blocks.Clear();
            if (string.IsNullOrWhiteSpace(markdown)) return;

            var lines = markdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            Paragraph? currentParagraph = null;

            foreach (var line in lines) {
                if (string.IsNullOrWhiteSpace(line)) {
                    currentParagraph = null; // Next line starts a new block
                    continue;
                }

                // 1. Headings
                if (line.StartsWith("# ")) {
                    targetTextBlock.Blocks.Add(CreateHeading(line.Substring(2), 24));
                    continue;
                }
                if (line.StartsWith("## ")) {
                    targetTextBlock.Blocks.Add(CreateHeading(line.Substring(3), 20));
                    continue;
                }
                if (line.StartsWith("### ")) {
                    targetTextBlock.Blocks.Add(CreateHeading(line.Substring(4), 16));
                    continue;
                }

                // 2. Lists
                if (line.StartsWith("- ") || line.StartsWith("* ")) {
                    var listPara = new Paragraph { Margin = new Thickness(20, 0, 0, 4) };
                    ParseInline(listPara, "• " + line.Substring(2));
                    targetTextBlock.Blocks.Add(listPara);
                    continue;
                }

                // 3. Normal Paragraph
                if (currentParagraph == null) {
                    currentParagraph = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
                    targetTextBlock.Blocks.Add(currentParagraph);
                }
                else {
                    currentParagraph.Inlines.Add(new LineBreak());
                }

                ParseInline(currentParagraph, line);
            }
        }

        private static Paragraph CreateHeading(string text, double fontSize) {
            var p = new Paragraph { Margin = new Thickness(0, 12, 0, 8) };
            var run = new Run { Text = text, FontSize = fontSize, FontWeight = FontWeights.Bold };
            p.Inlines.Add(run);
            return p;
        }

        private static void ParseInline(Paragraph paragraph, string text) {
            // Extremely simple inline parsing for **bold**.
            // For a highly concise implementation, we split by **.
            var parts = text.Split("**");
            for (int i = 0; i < parts.Length; i++) {
                if (i % 2 == 1 && i < parts.Length - 1) // It's bold (surrounded by **)
                {
                    var bold = new Bold();
                    bold.Inlines.Add(new Run { Text = parts[i] });
                    paragraph.Inlines.Add(bold);
                }
                else {
                    paragraph.Inlines.Add(new Run { Text = parts[i] });
                }
            }
        }
    }
}
