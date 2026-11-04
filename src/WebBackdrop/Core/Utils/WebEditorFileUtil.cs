using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Workloads.Creation.WebBackdrop.Core.Utils {
    public static class WebEditorFileUtil {
        public const string DefaultLanguage = "plaintext";
        public const string DefaultIconResourceKey = "WebBackdrop_FileTree_File";

        private static readonly Dictionary<string, string> ExtensionLanguageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            [".html"] = "html",
            [".htm"] = "html",
            [".css"] = "css",
            [".js"] = "javascript",
            [".json"] = "json",
            [".vpw"] = "vpw",
            [".ts"] = "typescript",
            [".xml"] = "xml",
            [".md"] = "markdown",
            [".markdown"] = "markdown",
            [".txt"] = DefaultLanguage,
        };

        private static readonly Dictionary<string, string> ExtensionIconResourceKeyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            [".html"] = "WebBackdrop_FileTree_Html",
            [".htm"] = "WebBackdrop_FileTree_Html",
            [".css"] = "WebBackdrop_FileTree_Css",
            [".js"] = "WebBackdrop_FileTree_Js",
            [".ts"] = "WebBackdrop_FileTree_Ts",
            [".jsx"] = "WebBackdrop_FileTree_Jsx",
            [".tsx"] = "WebBackdrop_FileTree_Tsx",
            [".json"] = "WebBackdrop_FileTree_Json",
            [".svg"] = "WebBackdrop_FileTree_Svg",
            [".png"] = "WebBackdrop_FileTree_Image",
            [".jpg"] = "WebBackdrop_FileTree_Image",
            [".jpeg"] = "WebBackdrop_FileTree_Image",
            [".gif"] = "WebBackdrop_FileTree_Image",
            [".webp"] = "WebBackdrop_FileTree_Image",
            [".bmp"] = "WebBackdrop_FileTree_Image",
            [".md"] = "WebBackdrop_FileTree_Md",
            [".markdown"] = "WebBackdrop_FileTree_Md",
        };

        private static readonly HashSet<string> PreviewImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".png",
            ".jpg",
            ".jpeg",
            ".gif",
            ".webp",
            ".bmp",
            ".svg",
        };

        public static string GetLanguageFromExtension(string extension) {
            return ExtensionLanguageMap.TryGetValue(extension, out var language) ? language : DefaultLanguage;
        }

        public static string GetManifestFileTypeFromExtension(string extension) {
            return ExtensionLanguageMap.TryGetValue(extension, out var language) ? language : "file";
        }

        public static string GetIconResourceKeyFromExtension(string extension) {
            return ExtensionIconResourceKeyMap.TryGetValue(extension, out var iconResourceKey) ? iconResourceKey : DefaultIconResourceKey;
        }

        public static bool IsTextExtension(string extension) {
            return ExtensionLanguageMap.ContainsKey(extension);
        }

        public static string FormatLanguage(string language) {
            return language switch {
                "html" => "HTML",
                "css" => "CSS",
                "javascript" => "JavaScript",
                "typescript" => "TypeScript",
                "json" => "JSON",
                "vpw" => "VPW",
                "xml" => "XML",
                "markdown" => "Markdown",
                _ => "Plain Text",
            };
        }

        public static string GetEditorLanguage(string language) {
            return language == "vpw" ? "json" : language;
        }

        public static bool IsPreviewImageExtension(string extension) {
            return PreviewImageExtensions.Contains(extension);
        }

        public static bool IsMarkdownExtension(string extension) {
            return GetLanguageFromExtension(extension) == "markdown";
        }

        public static int CountLines(string content) {
            if (string.IsNullOrEmpty(content)) return 0;

            // 单遍扫描计数，避免 Split 分配整行数组（大文件时更省内存）
            var count = 1;
            foreach (var character in content) {
                if (character == '\n') count++;
            }
            return count;
        }

        public static string RenderMarkdown(string markdown) {
            // 快速切换文件时上游可能传入 null（编辑器尚未就绪/内容尚未同步），
            // 统一按空文档处理，避免 NullReferenceException。
            markdown ??= string.Empty;

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
