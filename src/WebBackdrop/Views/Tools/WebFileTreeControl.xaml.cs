using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Others;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace Workloads.Creation.WebBackdrop.Views.Tools {
    public sealed partial class WebFileTreeControl : UserControl {
        public event EventHandler<string>? FileOpenRequested;
        public event EventHandler<string>? FolderSelected;
        public event EventHandler<string>? NewFileRequested;

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

            _projectFolder = projectFolder;
            var root = BuildDirectoryItem(projectFolder);
            root.IsExpanded = true;
            DataSource = new ObservableCollection<WebFileItem> {
                root,
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
            return BuildDirectoryItem(folderPath, null);
        }

        private static WebFileItem BuildDirectoryItem(string folderPath, WebFileItem? parent) {
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

        private async void NewFileMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            await CreateFileAsync(item.FilePath);
        }

        private async void NewFolderMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            await CreateFolderAsync(item.FilePath);
        }

        private void CutMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            _clipboardItem = item;
            _clipboardOperation = WebFileTreeClipboardOperation.Cut;
        }

        private void CopyMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            _clipboardItem = item;
            _clipboardOperation = WebFileTreeClipboardOperation.Copy;
        }

        private void PasteMenuItem_Click(object sender, RoutedEventArgs e) {
            var target = GetMenuItemTarget(sender);
            if (target == null || !CanPasteTo(target)) return;

            var source = _clipboardItem!;
            var destinationPath = GetAvailablePath(Path.Combine(target.FilePath, source.FileName));

            if (source.Type == WebFileItemType.File) {
                File.Copy(source.FilePath, destinationPath);
            }
            else if (_clipboardOperation == WebFileTreeClipboardOperation.Cut) {
                Directory.Move(source.FilePath, destinationPath);
            }
            else {
                CopyDirectory(source.FilePath, destinationPath);
            }

            if (_clipboardOperation == WebFileTreeClipboardOperation.Cut) {
                if (source.Type == WebFileItemType.File) {
                    File.Delete(source.FilePath);
                }
                RemoveItem(source);
                ClearClipboard();
            }
            else {
                _clipboardItem = BuildItem(source.FilePath, source.Type, source.Parent);
            }

            AddItem(target, BuildItem(destinationPath, source.Type, target));
            target.IsExpanded = true;
        }

        private void CopyPathMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            CopyToClipboard(item.FilePath);
        }

        private void CopyRelativePathMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            CopyToClipboard(GetRelativePath(item.FilePath));
        }

        private async void RenameMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            await RenameAsync(item);
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            DeletePath(item);
            RemoveItem(item);
            if (_clipboardItem == item) {
                ClearClipboard();
            }
        }

        private void RevealInExplorerMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            Process.Start("Explorer", "/select," + item.FilePath);
        }

        private void FolderMenuFlyout_Opening(object sender, object e) {
            if (sender is not MenuFlyout menuFlyout) return;

            foreach (var menuItem in menuFlyout.Items.OfType<MenuFlyoutItem>()) {
                if (menuItem.Text == "Paste" && menuItem.Tag is WebFileItem target) {
                    menuItem.IsEnabled = CanPasteTo(target);
                    return;
                }
            }
        }

        private async Task CreateFileAsync(string folderPath) {
            var path = await GetRenamedPathAsync(Path.Combine(folderPath, "New File.txt"));
            if (path == null) return;

            File.WriteAllText(path, string.Empty);
            AddItem(FindItem(folderPath), new WebFileItem(path, WebFileItemType.File, FindItem(folderPath)));
        }

        private async Task CreateFolderAsync(string folderPath) {
            var path = await GetRenamedPathAsync(Path.Combine(folderPath, "New Folder"));
            if (path == null) return;

            Directory.CreateDirectory(path);
            var parent = FindItem(folderPath);
            AddItem(parent, new WebFileItem(path, WebFileItemType.Folder, parent));
        }

        private async Task RenameAsync(WebFileItem item) {
            var path = await GetRenamedPathAsync(item.FilePath);
            if (path == null) return;

            if (item.Type == WebFileItemType.File) {
                File.Move(item.FilePath, path);
            }
            else {
                Directory.Move(item.FilePath, path);
            }

            ReplaceItem(item, BuildItem(path, item.Type, item.Parent));
        }

        private async Task<string?> GetRenamedPathAsync(string path) {
            var oldName = Path.GetFileName(path);
            var viewModel = new RenameViewModel(oldName);
            var dialogRes = await GlobalDialogUtils.ShowDialogAsync(
                new RenameView(viewModel),
                "Rename",
                "Confirm",
                "Cancel");

            if (dialogRes != DialogResult.Primary
                || !ComplianceUtil.IsValidValueOnlyLength(viewModel.NewName)
                || string.Equals(oldName, viewModel.NewName, StringComparison.Ordinal)) {
                return null;
            }

            var newName = viewModel.NewName!;
            if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;

            return GetAvailablePath(Path.Combine(Path.GetDirectoryName(path)!, newName));
        }

        private void AddItem(WebFileItem? parent, WebFileItem item) {
            var collection = parent?.Children ?? DataSource;
            if (collection == null) return;

            var index = 0;
            while (index < collection.Count && CompareFileItems(collection[index], item) <= 0) {
                index++;
            }
            collection.Insert(index, item);
            if (parent != null) {
                parent.IsExpanded = true;
            }
        }

        private void ReplaceItem(WebFileItem oldItem, WebFileItem newItem) {
            var collection = oldItem.Parent?.Children ?? DataSource;
            if (collection == null) return;

            var index = collection.IndexOf(oldItem);
            if (index < 0) return;

            collection.RemoveAt(index);
            AddItem(oldItem.Parent, newItem);
        }

        private void RemoveItem(WebFileItem item) {
            var collection = item.Parent?.Children ?? DataSource;
            collection?.Remove(item);
        }

        private static int CompareFileItems(WebFileItem left, WebFileItem right) {
            if (left.Type != right.Type) {
                return left.Type == WebFileItemType.Folder ? -1 : 1;
            }

            return string.Compare(left.FileName, right.FileName, StringComparison.OrdinalIgnoreCase);
        }

        private static WebFileItem BuildItem(string path, WebFileItemType type, WebFileItem? parent) {
            return type == WebFileItemType.Folder
                ? BuildDirectoryItem(path, parent)
                : new WebFileItem(path, WebFileItemType.File, parent);
        }

        private bool CanPasteTo(WebFileItem target) {
            if (_clipboardItem == null || _clipboardOperation == WebFileTreeClipboardOperation.None) return false;
            if (target.Type != WebFileItemType.Folder) return false;
            if (!File.Exists(_clipboardItem.FilePath) && !Directory.Exists(_clipboardItem.FilePath)) return false;
            if (_clipboardOperation == WebFileTreeClipboardOperation.Copy) return true;
            if (string.Equals(_clipboardItem.FilePath, target.FilePath, StringComparison.OrdinalIgnoreCase)) return false;
            if (_clipboardItem.Type == WebFileItemType.Folder) {
                var relativePath = Path.GetRelativePath(_clipboardItem.FilePath, target.FilePath);
                if (relativePath == "." || (!relativePath.StartsWith("..") && !Path.IsPathRooted(relativePath))) {
                    return false;
                }
            }

            return true;
        }

        private static void DeletePath(WebFileItem item) {
            if (item.Type == WebFileItemType.File) {
                File.Delete(item.FilePath);
            }
            else {
                Directory.Delete(item.FilePath, true);
            }
        }

        private static void CopyDirectory(string sourcePath, string destinationPath) {
            Directory.CreateDirectory(destinationPath);

            foreach (var directory in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories)) {
                Directory.CreateDirectory(directory.Replace(sourcePath, destinationPath));
            }

            foreach (var file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories)) {
                File.Copy(file, file.Replace(sourcePath, destinationPath));
            }
        }

        private static string GetAvailablePath(string path) {
            if (!File.Exists(path) && !Directory.Exists(path)) return path;

            var folder = Path.GetDirectoryName(path)!;
            var name = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);
            var index = 1;
            string availablePath;

            do {
                availablePath = Path.Combine(folder, $"{name} ({index}){extension}");
                index++;
            } while (File.Exists(availablePath) || Directory.Exists(availablePath));

            return availablePath;
        }

        private void ClearClipboard() {
            _clipboardItem = null;
            _clipboardOperation = WebFileTreeClipboardOperation.None;
        }

        private static WebFileItem? GetMenuItemTarget(object sender) {
            return sender is FrameworkElement { Tag: WebFileItem item } ? item : null;
        }

        private string GetRelativePath(string path) {
            return string.IsNullOrEmpty(_projectFolder)
                ? path
                : Path.GetRelativePath(_projectFolder, path);
        }

        private static void CopyToClipboard(string text) {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
        }

        private string _projectFolder = string.Empty;
        private WebFileItem? _clipboardItem;
        private WebFileTreeClipboardOperation _clipboardOperation;
    }

    public enum WebFileTreeClipboardOperation {
        None,
        Cut,
        Copy,
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
