using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using VirtualPaper.Models.Mvvm;

namespace Workloads.Creation.WebBackdrop.Views.Tools {
    public sealed partial class WebFileTreeControl : UserControl {
        public event EventHandler<string>? FileOpenRequested;
        public event EventHandler<string>? FolderSelected;
        public event EventHandler<string>? NewFileRequested;

        public string ProjectName {
            get => (string)GetValue(ProjectNameProperty);
            set => SetValue(ProjectNameProperty, value);
        }
        public static readonly DependencyProperty ProjectNameProperty =
            DependencyProperty.Register(nameof(ProjectName), typeof(string), typeof(WebFileTreeControl), new PropertyMetadata(string.Empty));

        public ObservableCollection<WebFileItem> DataSource {
            get => (ObservableCollection<WebFileItem>)GetValue(DataSourceProperty);
            set => SetValue(DataSourceProperty, value);
        }
        public static readonly DependencyProperty DataSourceProperty =
            DependencyProperty.Register(nameof(DataSource), typeof(ObservableCollection<WebFileItem>), typeof(WebFileTreeControl), new PropertyMetadata(null));

        public WebFileItem? SelectedFileItem {
            get => (WebFileItem?)GetValue(SelectedFileItemProperty);
            set => SetValue(SelectedFileItemProperty, value);
        }
        public static readonly DependencyProperty SelectedFileItemProperty =
            DependencyProperty.Register(nameof(SelectedFileItem), typeof(WebFileItem), typeof(WebFileTreeControl), new PropertyMetadata(null));

        public WebFileTreeControl() {
            InitializeComponent();
            PreloadFolderOpenIcon();
        }

        private static void PreloadFolderOpenIcon() {
            _ = Application.Current.Resources.TryGetValue("WebBackdrop_FileTree_FolderOpen", out _);
        }

        public void Refresh(string projectFolder) {
            if (!Directory.Exists(projectFolder)) return;

            DataSource = new ObservableCollection<WebFileItem> {
                BuildDirectoryItem(projectFolder),
            };
        }

        public void SelectFile(string filePath) {
            var item = FindItem(filePath);
            if (item == null) return;

            ExpandParents(item);
        }

        private WebFileItem? FindItem(string filePath) {
            return DataSource?.Select(root => FindItem(root, filePath)).FirstOrDefault(item => item != null);
        }

        private static WebFileItem? FindItem(WebFileItem item, string filePath) {
            if (string.Equals(item.FilePath, filePath, StringComparison.OrdinalIgnoreCase)) return item;

            foreach (var child in item.Children) {
                var result = FindItem(child, filePath);
                if (result != null) return result;
            }

            return null;
        }

        private static void ExpandParents(WebFileItem item) {
            var parent = item.Parent;
            while (parent != null) {
                parent.IsExpanded = true;
                parent = parent.Parent;
            }
        }

        private static WebFileItem BuildDirectoryItem(string folderPath) {
            var item = new WebFileItem(folderPath, WebFileItemType.Folder);

            foreach (var folder in Directory.GetDirectories(folderPath).OrderBy(Path.GetFileName)) {
                item.Children.Add(BuildDirectoryItem(folder, item));
            }

            foreach (var file in Directory.GetFiles(folderPath).OrderBy(Path.GetFileName)) {
                item.Children.Add(new WebFileItem(file, WebFileItemType.File, item));
            }

            return item;
        }

        private static WebFileItem BuildDirectoryItem(string folderPath, WebFileItem parent) {
            var item = new WebFileItem(folderPath, WebFileItemType.Folder, parent);

            foreach (var folder in Directory.GetDirectories(folderPath).OrderBy(Path.GetFileName)) {
                item.Children.Add(BuildDirectoryItem(folder, item));
            }

            foreach (var file in Directory.GetFiles(folderPath).OrderBy(Path.GetFileName)) {
                item.Children.Add(new WebFileItem(file, WebFileItemType.File, item));
            }

            return item;
        }

        private void FileTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args) {
            if (args.InvokedItem is WebFileItem { Type: WebFileItemType.Folder } folder) {
                folder.IsExpanded = !folder.IsExpanded;
                //SelectItem(folder);
                FolderSelected?.Invoke(this, folder.FilePath);
                return;
            }

            if (args.InvokedItem is WebFileItem { Type: WebFileItemType.File } item) {
                //SelectItem(item);
                FileOpenRequested?.Invoke(this, item.FilePath);
            }
        }

        private void NewFile_Click(object sender, RoutedEventArgs e) {
            NewFileRequested?.Invoke(this, EventArgs.Empty.ToString()!);
        }
    }

    public enum WebFileItemType {
        Folder,
        File,
    }

    public partial class WebFileItem : ObservableObject, IEquatable<WebFileItem> {
        public string FilePath { get; }
        public WebFileItemType Type { get; }
        public ObservableCollection<WebFileItem> Children { get; } = [];
        public WebFileItem? Parent { get; }
        public string FileName => Path.GetFileName(FilePath);
        public bool IsExpanded {
            get => _isExpanded;
            set {
                if (_isExpanded == value) return;
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FolderIconSource));
            }
        }
        public BitmapImage? FolderIconSource => Application.Current.Resources.TryGetValue(
            IsExpanded ? "WebBackdrop_FileTree_FolderOpen" : "WebBackdrop_FileTree_Folder", out var resource) && resource is BitmapImage image
            ? image
            : null;
        public BitmapImage? IconSource => Application.Current.Resources.TryGetValue(IconResourceKey, out var resource) && resource is BitmapImage image
            ? image
            : null;
        private string IconResourceKey => Path.GetExtension(FilePath).ToLowerInvariant() switch {
            ".html" or ".htm" => "WebBackdrop_FileTree_Html",
            ".css" => "WebBackdrop_FileTree_Css",
            ".js" => "WebBackdrop_FileTree_Js",
            ".ts" => "WebBackdrop_FileTree_Ts",
            ".jsx" => "WebBackdrop_FileTree_Jsx",
            ".tsx" => "WebBackdrop_FileTree_Tsx",
            ".json" => "WebBackdrop_FileTree_Json",
            ".svg" => "WebBackdrop_FileTree_Svg",
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" => "WebBackdrop_FileTree_Image",
            ".md" => "WebBackdrop_FileTree_Md",
            _ => "WebBackdrop_FileTree_File",
        };

        public WebFileItem(string filePath, WebFileItemType type, WebFileItem? parent = null) {
            FilePath = filePath;
            Type = type;
            Parent = parent;
        }

        public bool Equals(WebFileItem? other) {
            if (other == null) return false;
            return FilePath == other.FilePath && Type == other.Type;
        }

        public override bool Equals(object? obj) {
            return obj is WebFileItem other && Equals(other);
        }

        public override int GetHashCode() {
            return HashCode.Combine(FilePath, Type);
        }

        private bool _isExpanded;
    }

    class WebFileItemTemplateSelector : DataTemplateSelector {
        public DataTemplate? FolderTemplate { get; set; }
        public DataTemplate? FileTemplate { get; set; }

        protected override DataTemplate? SelectTemplateCore(object item) {
            var fileItem = (WebFileItem)item;

            return fileItem.Type == WebFileItemType.Folder
                ? FolderTemplate
                : FileTemplate;
        }
    }
}
