using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.Models.SerializableData;

namespace Workloads.Creation.WebBackdrop.Views.Tools {
    public sealed partial class WebPropertyPanelControl : UserControl {
        private string? _projectFolder;
        private string? _currentPath;
        private string? _pendingPreviewHtml;

        public WebPropertyPanelControl() {
            InitializeComponent();
            Loaded += WebPropertyPanelControl_Loaded;
        }

        private void WebPropertyPanelControl_Loaded(object sender, RoutedEventArgs e) {
            if (_pendingPreviewHtml != null) {
                _ = NavigatePreviewAsync(_pendingPreviewHtml);
            }
        }

        public void LoadProject(WebDesignFileUtil designFileUtil) {
            _projectFolder = designFileUtil.ProjectFolder;
            var data = designFileUtil.GetOrCreateProjectData();

            ProjectTitleText.Text = string.IsNullOrWhiteSpace(data.Title) ? designFileUtil.ProjectName : data.Title;
            ProjectEntryText.Text = data.File;
            ProjectPathText.Text = designFileUtil.ProjectFolder;
        }

        public void Load(WebEditorFile? file, string language) {
            if (file == null) {
                ClearCurrentItem();
                return;
            }

            _currentPath = file.FilePath;
            var info = new FileInfo(file.FilePath);
            FileNameText.Text = file.FileName;
            TypeLabel.Text = "Language";
            TypeText.Text = language;
            StatusText.Text = file.IsSaved ? "Saved" : "Unsaved";
            SizeText.Text = info.Exists ? FormatSize(info.Length) : "-";
            LineCountText.Text = CountLines(file.Content).ToString();
            ModifiedText.Text = info.Exists ? info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") : "-";
            RelativePathText.Text = GetRelativePath(file.FilePath);
            PathText.Text = file.FilePath;

            NoItemPanel.Visibility = Visibility.Collapsed;
            ItemInfoPanel.Visibility = Visibility.Visible;
            StatusRow.Visibility = Visibility.Visible;
            SizeRow.Visibility = Visibility.Visible;
            LineCountRow.Visibility = Visibility.Visible;

            LoadPreview(file);
        }

        public void LoadFolder(string folderPath) {
            _currentPath = folderPath;
            var info = new DirectoryInfo(folderPath);
            FileNameText.Text = info.Exists ? info.Name : Path.GetFileName(folderPath);
            TypeLabel.Text = "Type";
            TypeText.Text = "Folder";
            ModifiedText.Text = info.Exists ? info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") : "-";
            RelativePathText.Text = GetRelativePath(folderPath);
            PathText.Text = folderPath;

            NoItemPanel.Visibility = Visibility.Collapsed;
            ItemInfoPanel.Visibility = Visibility.Visible;
            StatusRow.Visibility = Visibility.Collapsed;
            SizeRow.Visibility = Visibility.Collapsed;
            LineCountRow.Visibility = Visibility.Collapsed;

            SetPreviewMessage("Select an image, SVG, or Markdown file to preview.");
        }

        public void Clear() {
            ProjectTitleText.Text = "-";
            ProjectEntryText.Text = "-";
            ProjectPathText.Text = "-";
            ClearCurrentItem();
        }

        private void ClearCurrentItem() {
            _currentPath = null;
            NoItemPanel.Visibility = Visibility.Visible;
            ItemInfoPanel.Visibility = Visibility.Collapsed;
            SetPreviewMessage("No preview available.");
        }

        private void CopyPath_Click(object sender, RoutedEventArgs e) {
            if (string.IsNullOrEmpty(_currentPath)) return;

            var package = new DataPackage();
            package.SetText(_currentPath);
            Clipboard.SetContent(package);
        }

        private void Reveal_Click(object sender, RoutedEventArgs e) {
            if (string.IsNullOrEmpty(_currentPath)) return;

            Process.Start("Explorer", "/select," + _currentPath);
        }

        private void LoadPreview(WebEditorFile file) {
            if (IsImage(file.FileExtension)) {
                var uri = new Uri(file.FilePath).AbsoluteUri;
                NavigatePreview($$"""
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<style>
html,body{margin:0;width:100%;height:100%;background:#1e1e1e;display:flex;align-items:center;justify-content:center;}
img{max-width:100%;max-height:100%;object-fit:contain;}
</style>
</head>
<body><img src="{{WebUtility.HtmlEncode(uri)}}"></body>
</html>
""");
                return;
            }

            if (file.FileExtension == ".md" || file.FileExtension == ".markdown") {
                NavigatePreview($$"""
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<style>
body{font-family:'Segoe UI',sans-serif;margin:16px;line-height:1.55;color:#f3f3f3;background:#1e1e1e;}
h1,h2,h3,h4,h5,h6{line-height:1.25;}
pre,code{font-family:Consolas,monospace;background:#2d2d2d;border-radius:4px;}
code{padding:2px 4px;}
pre{padding:10px;overflow:auto;}
blockquote{border-left:3px solid #777;margin-left:0;padding-left:10px;color:#ccc;}
a{color:#8ab4f8;}
</style>
</head>
<body>{{RenderMarkdown(file.Content)}}</body>
</html>
""");
                return;
            }

            SetPreviewMessage("Preview supports images, SVG, and Markdown.");
        }

        private void SetPreviewMessage(string message) {
            NavigatePreview($$"""
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<style>
html,body{margin:0;width:100%;height:100%;background:#1e1e1e;color:#aaa;font-family:'Segoe UI',sans-serif;display:flex;align-items:center;justify-content:center;text-align:center;padding:12px;box-sizing:border-box;}
</style>
</head>
<body>{{WebUtility.HtmlEncode(message)}}</body>
</html>
""");
        }

        private async void NavigatePreview(string html) {
            _pendingPreviewHtml = html;
            if (!IsLoaded) return;

            await NavigatePreviewAsync(html);
        }

        private async Task NavigatePreviewAsync(string html) {
            await previewWebView.EnsureCoreWebView2Async();
            if (_pendingPreviewHtml == html) {
                previewWebView.NavigateToString(html);
            }
        }

        private string GetRelativePath(string path) {
            return string.IsNullOrEmpty(_projectFolder)
                ? path
                : Path.GetRelativePath(_projectFolder, path);
        }

        private static string RenderMarkdown(string markdown) {
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

        private static bool IsImage(string extension) {
            return extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" or ".svg";
        }

        private static int CountLines(string content) {
            if (string.IsNullOrEmpty(content)) return 0;
            return content.Split('\n').Length;
        }

        private static string FormatSize(long bytes) {
            string[] units = ["B", "KB", "MB", "GB"];
            var size = (double)bytes;
            var unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1) {
                size /= 1024;
                unitIndex++;
            }

            return unitIndex == 0
                ? $"{bytes} {units[unitIndex]}"
                : $"{size:0.##} {units[unitIndex]}";
        }
    }
}
