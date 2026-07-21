using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Models.Mvvm;
using Microsoft.UI.Xaml;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.Models.SerializableData;

namespace Workloads.Creation.WebBackdrop.ViewModels {
    public partial class WebPropertyPanelViewModel : ObservableObject {
        public string ProjectTitleText {
            get => _projectTitleText;
            private set { if (_projectTitleText == value) return; _projectTitleText = value; OnPropertyChanged(); }
        }

        public string ProjectEntryText {
            get => _projectEntryText;
            private set { if (_projectEntryText == value) return; _projectEntryText = value; OnPropertyChanged(); }
        }

        public string FileNameText {
            get => _fileNameText;
            private set { if (_fileNameText == value) return; _fileNameText = value; OnPropertyChanged(); }
        }

        public string TypeLabelText {
            get => _typeLabelText;
            private set { if (_typeLabelText == value) return; _typeLabelText = value; OnPropertyChanged(); }
        }

        public string TypeText {
            get => _typeText;
            private set { if (_typeText == value) return; _typeText = value; OnPropertyChanged(); }
        }

        public string StatusText {
            get => _statusText;
            private set { if (_statusText == value) return; _statusText = value; OnPropertyChanged(); }
        }

        public string SizeText {
            get => _sizeText;
            private set { if (_sizeText == value) return; _sizeText = value; OnPropertyChanged(); }
        }

        public string LineCountText {
            get => _lineCountText;
            private set { if (_lineCountText == value) return; _lineCountText = value; OnPropertyChanged(); }
        }

        public string ModifiedText {
            get => _modifiedText;
            private set { if (_modifiedText == value) return; _modifiedText = value; OnPropertyChanged(); }
        }

        public string RelativePathText {
            get => _relativePathText;
            private set { if (_relativePathText == value) return; _relativePathText = value; OnPropertyChanged(); }
        }

        public Visibility NoItemVisibility {
            get => _noItemVisibility;
            private set { if (_noItemVisibility == value) return; _noItemVisibility = value; OnPropertyChanged(); }
        }

        public Visibility ItemInfoVisibility {
            get => _itemInfoVisibility;
            private set { if (_itemInfoVisibility == value) return; _itemInfoVisibility = value; OnPropertyChanged(); }
        }

        public Visibility StatusVisibility {
            get => _statusVisibility;
            private set { if (_statusVisibility == value) return; _statusVisibility = value; OnPropertyChanged(); }
        }

        public Visibility SizeVisibility {
            get => _sizeVisibility;
            private set { if (_sizeVisibility == value) return; _sizeVisibility = value; OnPropertyChanged(); }
        }

        public Visibility LineCountVisibility {
            get => _lineCountVisibility;
            private set { if (_lineCountVisibility == value) return; _lineCountVisibility = value; OnPropertyChanged(); }
        }

        public string? PreviewHtml {
            get => _previewHtml;
            private set { if (_previewHtml == value) return; _previewHtml = value; OnPropertyChanged(); }
        }

        public void SetPreviewTheme(WebPropertyPanelPreviewTheme theme) {
            _previewTheme = theme;
            RefreshPreview();
        }

        public void LoadProject(WebDesignFileUtil designFileUtil) {
            _projectFolder = designFileUtil.ProjectFolder;
            var data = designFileUtil.GetOrCreateProjectData();

            ProjectTitleText = string.IsNullOrWhiteSpace(data.Title) ? designFileUtil.ProjectName : data.Title;
            ProjectEntryText = data.File;
        }

        public void Load(WebEditorFile? file, string language) {
            if (file == null) {
                ClearCurrentItem();
                return;
            }

            _currentFile = file;
            var info = new FileInfo(file.FilePath);
            FileNameText = file.FileName;
            TypeLabelText = "Language";
            TypeText = language;
            StatusText = file.IsSaved ? "Saved" : "Unsaved";
            SizeText = info.Exists ? FileUtil.SizeSuffix(info.Length) : "-";
            LineCountText = WebEditorFileUtil.CountLines(file.Content).ToString();
            ModifiedText = info.Exists ? info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") : "-";
            RelativePathText = FileUtil.GetRelativePath(_projectFolder, file.FilePath);

            NoItemVisibility = Visibility.Collapsed;
            ItemInfoVisibility = Visibility.Visible;
            StatusVisibility = Visibility.Visible;
            SizeVisibility = Visibility.Visible;
            LineCountVisibility = Visibility.Visible;

            LoadPreview(file);
        }

        public void LoadFolder(string folderPath) {
            _currentFile = null;
            var info = new DirectoryInfo(folderPath);
            FileNameText = info.Exists ? info.Name : Path.GetFileName(folderPath);
            TypeLabelText = "Type";
            TypeText = "Folder";
            ModifiedText = info.Exists ? info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") : "-";
            RelativePathText = FileUtil.GetRelativePath(_projectFolder, folderPath);

            NoItemVisibility = Visibility.Collapsed;
            ItemInfoVisibility = Visibility.Visible;
            StatusVisibility = Visibility.Collapsed;
            SizeVisibility = Visibility.Collapsed;
            LineCountVisibility = Visibility.Collapsed;

            SetPreviewMessage("Select an image, SVG, or Markdown file to preview.");
        }

        public void Clear() {
            ProjectTitleText = "-";
            ProjectEntryText = "-";
            ClearCurrentItem();
        }

        private void ClearCurrentItem() {
            _currentFile = null;
            NoItemVisibility = Visibility.Visible;
            ItemInfoVisibility = Visibility.Collapsed;
            SetPreviewMessage("No preview available.");
        }

        private void RefreshPreview() {
            if (_currentFile != null) {
                LoadPreview(_currentFile);
            }
            else if (NoItemVisibility == Visibility.Visible) {
                SetPreviewMessage("No preview available.");
            }
            else {
                SetPreviewMessage("Select an image, SVG, or Markdown file to preview.");
            }
        }

        private void LoadPreview(WebEditorFile file) {
            if (WebEditorFileUtil.IsPreviewImageExtension(file.FileExtension)) {
                PreviewHtml = RenderPreviewTemplate("image-preview.html", new Dictionary<string, string> {
                    ["ImageUri"] = WebUtility.HtmlEncode(new Uri(file.FilePath).AbsoluteUri),
                });
                return;
            }

            if (WebEditorFileUtil.IsMarkdownExtension(file.FileExtension)) {
                PreviewHtml = RenderPreviewTemplate("markdown-preview.html", new Dictionary<string, string> {
                    ["MarkdownHtml"] = WebEditorFileUtil.RenderMarkdown(file.Content),
                });
                return;
            }

            SetPreviewMessage("Preview supports images, SVG, and Markdown.");
        }

        private void SetPreviewMessage(string message) {
            PreviewHtml = RenderPreviewTemplate("message-preview.html", new Dictionary<string, string> {
                ["Message"] = WebUtility.HtmlEncode(message),
            });
        }

        private string RenderPreviewTemplate(string templateName, IReadOnlyDictionary<string, string> values) {
            var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, Constants.ModuleName.WebBackdrop, "Assets", "preview", templateName));
            foreach (var pair in GetPreviewTemplateValues(values)) {
                html = html.Replace("{{" + pair.Key + "}}", pair.Value);
            }

            return html;
        }

        private Dictionary<string, string> GetPreviewTemplateValues(IReadOnlyDictionary<string, string> values) {
            var templateValues = new Dictionary<string, string> {
                ["PreviewBackground"] = _previewTheme.PreviewBackground,
                ["PreviewForeground"] = _previewTheme.PreviewForeground,
                ["PreviewSecondaryForeground"] = _previewTheme.PreviewSecondaryForeground,
                ["PreviewCodeBackground"] = _previewTheme.PreviewCodeBackground,
                ["PreviewQuoteBorder"] = _previewTheme.PreviewQuoteBorder,
                ["PreviewLinkForeground"] = _previewTheme.PreviewLinkForeground,
            };

            foreach (var pair in values) {
                templateValues[pair.Key] = pair.Value;
            }

            return templateValues;
        }

        private string _projectFolder = string.Empty;
        private WebEditorFile? _currentFile;
        private WebPropertyPanelPreviewTheme _previewTheme = WebPropertyPanelPreviewTheme.Empty;
        private string _projectTitleText = "-";
        private string _projectEntryText = "-";
        private string _fileNameText = string.Empty;
        private string _typeLabelText = "Type";
        private string _typeText = string.Empty;
        private string _statusText = string.Empty;
        private string _sizeText = string.Empty;
        private string _lineCountText = string.Empty;
        private string _modifiedText = string.Empty;
        private string _relativePathText = string.Empty;
        private Visibility _noItemVisibility = Visibility.Visible;
        private Visibility _itemInfoVisibility = Visibility.Collapsed;
        private Visibility _statusVisibility = Visibility.Visible;
        private Visibility _sizeVisibility = Visibility.Visible;
        private Visibility _lineCountVisibility = Visibility.Visible;
        private string? _previewHtml;
    }

    public readonly record struct WebPropertyPanelPreviewTheme(
        string PreviewBackground,
        string PreviewForeground,
        string PreviewSecondaryForeground,
        string PreviewCodeBackground,
        string PreviewQuoteBorder,
        string PreviewLinkForeground) {
        public static WebPropertyPanelPreviewTheme Empty { get; } = new(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
    }
}
