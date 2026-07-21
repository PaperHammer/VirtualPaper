using System.Net;
using System.Text;

namespace Workloads.Creation.WebBackdrop.Core.Utils {
    public static class WebEditorFileUtil {
        public static string GetLanguageFromExtension(string extension) {
            return extension.ToLowerInvariant() switch {
                ".html" or ".htm" => "html",
                ".css" => "css",
                ".js" => "javascript",
                ".json" => "json",
                ".ts" => "typescript",
                ".xml" => "xml",
                ".md" => "markdown",
                _ => "plaintext",
            };
        }

        public static string FormatLanguage(string language) {
            return language switch {
                "html" => "HTML",
                "css" => "CSS",
                "javascript" => "JavaScript",
                "typescript" => "TypeScript",
                "json" => "JSON",
                "xml" => "XML",
                "markdown" => "Markdown",
                _ => "Plain Text",
            };
        }

        public static bool IsPreviewImageExtension(string extension) {
            return extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" or ".svg";
        }

        public static bool IsMarkdownExtension(string extension) {
            return extension is ".md" or ".markdown";
        }

        public static int CountLines(string content) {
            if (string.IsNullOrEmpty(content)) return 0;
            return content.Split('\n').Length;
        }

        public static string RenderMarkdown(string markdown) {
            var html = new StringBuilder();
            var inCodeBlock = false;

            foreach (var rawLine in markdown.Replace("\r\n", "\n").Split('\n')) {
                var line = rawLine.TrimEnd();

                if (line.StartsWith("```")) {
                    html.Append(inCodeBlock ? "</code></pre>" : "<pre><code>");
                    inCodeBlock = !inCodeBlock;
                    continue;
                }

                if (inCodeBlock) {
                    html.Append(WebUtility.HtmlEncode(line)).Append('\n');
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line)) {
                    continue;
                }

                var headingLevel = GetHeadingLevel(line);
                if (headingLevel > 0) {
                    var text = line[headingLevel..].Trim();
                    html.Append($"<h{headingLevel}>{FormatInlineMarkdown(text)}</h{headingLevel}>");
                    continue;
                }

                if (line.StartsWith("> ")) {
                    html.Append("<blockquote>").Append(FormatInlineMarkdown(line[2..])).Append("</blockquote>");
                    continue;
                }

                html.Append("<p>").Append(FormatInlineMarkdown(line)).Append("</p>");
            }

            if (inCodeBlock) {
                html.Append("</code></pre>");
            }

            return html.ToString();
        }

        private static int GetHeadingLevel(string line) {
            var level = 0;
            while (level < line.Length && level < 6 && line[level] == '#') {
                level++;
            }

            return level > 0 && level < line.Length && line[level] == ' ' ? level : 0;
        }

        private static string FormatInlineMarkdown(string text) {
            return WebUtility.HtmlEncode(text)
                .Replace("**", string.Empty)
                .Replace("__", string.Empty)
                .Replace("`", string.Empty);
        }
    }
}
