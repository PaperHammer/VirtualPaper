using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using Windows.ApplicationModel.DataTransfer;
using Workloads.Creation.WebBackdrop.Core.Theme;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.Models.SerializableData;

namespace Workloads.Creation.WebBackdrop.Views.Tools {
    public sealed partial class WebPropertyPanelControl : UserControl {
        private string? _projectFolder;
        private string? _currentPath;
        private WebEditorFile? _currentFile;
        private string? _pendingPreviewHtml;

        public WebPropertyPanelControl() {
            InitializeComponent();
            Loaded += WebPropertyPanelControl_Loaded;
            ActualThemeChanged += WebPropertyPanelControl_ActualThemeChanged;
            UpdatePreviewBackground();
        }

        private void WebPropertyPanelControl_Loaded(object sender, RoutedEventArgs e) {
            UpdatePreviewBackground();
            UpdatePreviewHeight();
            if (_pendingPreviewHtml != null) {
                _ = NavigatePreviewAsync(_pendingPreviewHtml);
            }
        }

        private void WebPropertyPanelControl_ActualThemeChanged(FrameworkElement sender, object args) {
            UpdatePreviewBackground();
            if (_currentFile != null) {
                LoadPreview(_currentFile);
                return;
            }

            SetPreviewMessage(NoItemPanel.Visibility == Visibility.Visible
                ? "No preview available."
                : "Select an image, SVG, or Markdown file to preview.");
        }

        private void UpdatePreviewBackground() {
            var role = IsLightTheme
                ? WebBackdropColorRole.WebViewLightBackground
                : WebBackdropColorRole.WebViewDarkBackground;
            previewWebView.DefaultBackgroundColor = WebBackdropThemeResource.GetColor(this, role);
        }

        private bool IsLightTheme => ActualTheme == ElementTheme.Light;
        private bool IsCurrentPreviewImage => _currentFile != null && IsImage(_currentFile.FileExtension);
        private string PreviewBackground => GetPreviewString(WebBackdropStringRole.PreviewLightBackground, WebBackdropStringRole.PreviewDarkBackground);
        private string PreviewForeground => GetPreviewString(WebBackdropStringRole.PreviewLightForeground, WebBackdropStringRole.PreviewDarkForeground);
        private string PreviewSecondaryForeground => GetPreviewString(WebBackdropStringRole.PreviewLightSecondaryForeground, WebBackdropStringRole.PreviewDarkSecondaryForeground);
        private string PreviewCodeBackground => GetPreviewString(WebBackdropStringRole.PreviewLightCodeBackground, WebBackdropStringRole.PreviewDarkCodeBackground);
        private string PreviewQuoteBorder => GetPreviewString(WebBackdropStringRole.PreviewLightQuoteBorder, WebBackdropStringRole.PreviewDarkQuoteBorder);
        private string PreviewLinkForeground => GetPreviewString(WebBackdropStringRole.PreviewLightLinkForeground, WebBackdropStringRole.PreviewDarkLinkForeground);

        private string GetPreviewString(WebBackdropStringRole lightRole, WebBackdropStringRole darkRole) {
            return WebBackdropThemeResource.GetString(this, IsLightTheme ? lightRole : darkRole);
        }

        private void PreviewWebViewHost_SizeChanged(object sender, SizeChangedEventArgs e) => UpdatePreviewHeight(e.NewSize.Width);

        private void UpdatePreviewHeight(double width = 0) {
            var previewWidth = width > 0 ? width : PreviewWebViewHost.ActualWidth;
            if (previewWidth <= 0) return;

            PreviewWebViewHost.Height = Math.Max(160, previewWidth * 10 / 16);
        }

        public void LoadProject(WebDesignFileUtil designFileUtil) {
            _projectFolder = designFileUtil.ProjectFolder;
            var data = designFileUtil.GetOrCreateProjectData();

            ProjectTitleText.Text = string.IsNullOrWhiteSpace(data.Title) ? designFileUtil.ProjectName : data.Title;
            ProjectEntryText.Text = data.File;
            //ProjectPathText.Text = designFileUtil.ProjectFolder;
        }

        public void Load(WebEditorFile? file, string language) {
            if (file == null) {
                ClearCurrentItem();
                return;
            }

            _currentPath = file.FilePath;
            _currentFile = file;
            UpdatePreviewHeight();
            var info = new FileInfo(file.FilePath);
            FileNameText.Text = file.FileName;
            TypeLabel.Text = "Language";
            TypeText.Text = language;
            StatusText.Text = file.IsSaved ? "Saved" : "Unsaved";
            SizeText.Text = info.Exists ? FormatSize(info.Length) : "-";
            LineCountText.Text = CountLines(file.Content).ToString();
            ModifiedText.Text = info.Exists ? info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") : "-";
            RelativePathText.Text = GetRelativePath(file.FilePath);
            //PathText.Text = file.FilePath;

            NoItemPanel.Visibility = Visibility.Collapsed;
            ItemInfoPanel.Visibility = Visibility.Visible;
            StatusRow.Visibility = Visibility.Visible;
            SizeRow.Visibility = Visibility.Visible;
            LineCountRow.Visibility = Visibility.Visible;

            LoadPreview(file);
        }

        public void LoadFolder(string folderPath) {
            _currentPath = folderPath;
            _currentFile = null;
            UpdatePreviewHeight();
            var info = new DirectoryInfo(folderPath);
            FileNameText.Text = info.Exists ? info.Name : Path.GetFileName(folderPath);
            TypeLabel.Text = "Type";
            TypeText.Text = "Folder";
            ModifiedText.Text = info.Exists ? info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") : "-";
            RelativePathText.Text = GetRelativePath(folderPath);
            //PathText.Text = folderPath;

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
            //ProjectPathText.Text = "-";
            ClearCurrentItem();
        }

        private void ClearCurrentItem() {
            _currentPath = null;
            _currentFile = null;
            UpdatePreviewHeight();
            NoItemPanel.Visibility = Visibility.Visible;
            ItemInfoPanel.Visibility = Visibility.Collapsed;
            SetPreviewMessage("No preview available.");
        }

        //private void CopyPath_Click(object sender, RoutedEventArgs e) {
        //    if (string.IsNullOrEmpty(_currentPath)) return;

        //    var package = new DataPackage();
        //    package.SetText(_currentPath);
        //    Clipboard.SetContent(package);
        //}

        //private void Reveal_Click(object sender, RoutedEventArgs e) {
        //    if (string.IsNullOrEmpty(_currentPath)) return;

        //    Process.Start("Explorer", "/select," + _currentPath);
        //}

        private void LoadPreview(WebEditorFile file) {
            if (IsImage(file.FileExtension)) {
                NavigatePreview(RenderPreviewTemplate("image-preview.html", new Dictionary<string, string> {
                    ["ImageUri"] = WebUtility.HtmlEncode(new Uri(file.FilePath).AbsoluteUri),
                }));
                return;
            }

            if (file.FileExtension == ".md" || file.FileExtension == ".markdown") {
                NavigatePreview(RenderPreviewTemplate("markdown-preview.html", new Dictionary<string, string> {
                    ["MarkdownHtml"] = RenderMarkdown(file.Content),
                }));
                return;
            }

            SetPreviewMessage("Preview supports images, SVG, and Markdown.");
        }

        private void SetPreviewMessage(string message) {
            NavigatePreview(RenderPreviewTemplate("message-preview.html", new Dictionary<string, string> {
                ["Message"] = WebUtility.HtmlEncode(message),
            }));
        }

        private string RenderPreviewTemplate(string templateName, IReadOnlyDictionary<string, string> values) {
            var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, Constants.ModuleName.WebBackdrop, "Assets", "preview", templateName));
            foreach (var pair in GetPreviewTemplateValues(values)) {
                html = html.Replace("{{" + pair.Key + "}}", pair.Value);
            }

            return html;
        }

        private IReadOnlyDictionary<string, string> GetPreviewTemplateValues(IReadOnlyDictionary<string, string> values) {
            var templateValues = new Dictionary<string, string> {
                ["PreviewBackground"] = PreviewBackground,
                ["PreviewForeground"] = PreviewForeground,
                ["PreviewSecondaryForeground"] = PreviewSecondaryForeground,
                ["PreviewCodeBackground"] = PreviewCodeBackground,
                ["PreviewQuoteBorder"] = PreviewQuoteBorder,
                ["PreviewLinkForeground"] = PreviewLinkForeground,
            };

            foreach (var pair in values) {
                templateValues[pair.Key] = pair.Value;
            }

            return templateValues;
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
