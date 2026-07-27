using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Models.Mvvm;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.Models.SerializableData;

namespace Workloads.Creation.WebBackdrop.ViewModels {
    public partial class WebFileTreeViewModel : ObservableObject {
        public ObservableCollection<WebFileItem> FileItems = [];

        public string ProjectFolder {
            get => _projectFolder;
            private set {
                if (_projectFolder == value) return;
                _projectFolder = value;
                OnPropertyChanged();
            }
        }

        public void Refresh(WebDesignFileUtil designFileUtil) {
            if (!Directory.Exists(designFileUtil.ProjectFolder)) return;

            _designFileUtil = designFileUtil;
            _manifestItems = designFileUtil.GetManifestItems();
            ProjectFolder = designFileUtil.ProjectFolder;
            var root = CreateDirectoryItem(ProjectFolder, null);
            LoadChildren(root);
            root.IsExpanded = true;
            FileItems.Clear();
            FileItems.Add(root);
        }

        public void Refresh(string projectFolder) {
            if (!Directory.Exists(projectFolder)) return;

            _designFileUtil = null;
            _manifestItems = null;
            ProjectFolder = projectFolder;
            var root = CreateDirectoryItem(projectFolder, null);
            LoadChildren(root);
            root.IsExpanded = true;
            FileItems.Clear();
            FileItems.Add(root);
        }

        public void SelectFile(string filePath) {
            var item = FindOrLoadItem(filePath);
            if (item == null) return;

            ExpandParents(item);
        }

        public void SetFileSaved(string filePath, bool isSaved) {
            var item = FindOrLoadItem(filePath);
            if (item == null) return;

            item.IsSaved = isSaved;
        }

        public void ToggleFolder(WebFileItem folder) {
            if (folder.Type != WebFileItemType.Folder) return;

            if (!folder.IsExpanded) {
                LoadChildren(folder);
            }
            folder.IsExpanded = !folder.IsExpanded;
        }

        public async Task CreateFileAsync(string folderPath, Func<string, Task<string?>> getRenamedPathAsync) {
            var path = await getRenamedPathAsync(Path.Combine(folderPath, "New File.txt"));
            if (path == null) return;

            File.WriteAllText(path, string.Empty);
            _designFileUtil?.AddManifestPath(path);
            var parent = FindOrLoadItem(folderPath);
            AddItem(parent, new WebFileItem(path, WebFileItemType.File, parent));
        }

        public async Task CreateFolderAsync(string folderPath, Func<string, Task<string?>> getRenamedPathAsync) {
            var path = await getRenamedPathAsync(Path.Combine(folderPath, "New Folder"));
            if (path == null) return;

            Directory.CreateDirectory(path);
            _designFileUtil?.AddManifestPath(path);
            var parent = FindOrLoadItem(folderPath);
            AddItem(parent, CreateDirectoryItem(path, parent));
        }

        public async Task RenameAsync(WebFileItem item, Func<string, Task<string?>> getRenamedPathAsync) {
            var path = await getRenamedPathAsync(item.FilePath);
            if (path == null) return;

            var oldPath = item.FilePath;
            if (item.Type == WebFileItemType.File) {
                File.Move(oldPath, path);
            }
            else {
                Directory.Move(oldPath, path);
            }

            _designFileUtil?.RenameManifestPath(oldPath, path);
            ReplaceItem(item, BuildItem(path, item.Type, item.Parent));
        }

        public void Cut(WebFileItem item) {
            _clipboardItem = item;
            _clipboardOperation = WebFileTreeClipboardOperation.Cut;
        }

        public void Copy(WebFileItem item) {
            _clipboardItem = item;
            _clipboardOperation = WebFileTreeClipboardOperation.Copy;
        }

        public void PasteTo(WebFileItem target) {
            if (!CanPasteTo(target)) return;

            var source = _clipboardItem!;
            var destinationPath = FileUtil.NextAvailablePath(Path.Combine(target.FilePath, source.FileName));

            if (source.Type == WebFileItemType.File) {
                File.Copy(source.FilePath, destinationPath);
            }
            else if (_clipboardOperation == WebFileTreeClipboardOperation.Cut) {
                Directory.Move(source.FilePath, destinationPath);
            }
            else {
                FileUtil.CopyDirectory(source.FilePath, destinationPath, true);
            }

            if (_clipboardOperation == WebFileTreeClipboardOperation.Cut) {
                _designFileUtil?.RenameManifestPath(source.FilePath, destinationPath);
                if (source.Type == WebFileItemType.File) {
                    File.Delete(source.FilePath);
                }
                RemoveItem(source);
                ClearClipboard();
            }
            else {
                _designFileUtil?.AddManifestPathRecursive(destinationPath);
                _clipboardItem = BuildItem(source.FilePath, source.Type, source.Parent);
            }

            AddItem(target, BuildItem(destinationPath, source.Type, target));
            LoadChildren(target);
            target.IsExpanded = true;
        }

        public void Delete(WebFileItem item) {
            _designFileUtil?.RemoveManifestPath(item.FilePath);
            DeletePath(item);
            RemoveItem(item);
            if (_clipboardItem == item) {
                ClearClipboard();
            }
        }

        public bool CanPasteTo(WebFileItem target) {
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

        private WebFileItem? FindItem(string filePath) {
            return FileItems.Select(root => FindItem(root, filePath)).FirstOrDefault(item => item != null);
        }

        private WebFileItem? FindOrLoadItem(string filePath) {
            var item = FindItem(filePath);
            if (item != null) return item;

            var root = FileItems.FirstOrDefault();
            if (root == null) return null;

            var relativePath = Path.GetRelativePath(ProjectFolder, filePath);
            if (relativePath == ".") return root;
            if (relativePath.StartsWith("..") || Path.IsPathRooted(relativePath)) return null;

            var current = root;
            foreach (var part in relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) {
                if (string.IsNullOrEmpty(part)) continue;
                LoadChildren(current);
                current = current.Children.FirstOrDefault(child => string.Equals(child.FileName, part, StringComparison.OrdinalIgnoreCase));
                if (current == null) return null;
            }

            return current;
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

        private WebFileItem CreateDirectoryItem(string folderPath, WebFileItem? parent) {
            var item = new WebFileItem(folderPath, WebFileItemType.Folder, parent);
            if (HasChildren(folderPath)) {
                item.Children.Add(new WebFileItem(Path.Combine(folderPath, PlaceholderFileName), WebFileItemType.File, item));
            }
            return item;
        }

        private void LoadChildren(WebFileItem folder) {
            if (folder.Type != WebFileItemType.Folder || folder.IsChildrenLoaded) return;

            folder.Children.Clear();
            if (_manifestItems != null) {
                LoadManifestChildren(folder);
            }
            else {
                LoadDirectoryChildren(folder);
            }
            folder.IsChildrenLoaded = true;
        }

        private void LoadDirectoryChildren(WebFileItem folder) {
            foreach (var childFolder in Directory.GetDirectories(folder.FilePath).OrderBy(Path.GetFileName)) {
                folder.Children.Add(CreateDirectoryItem(childFolder, folder));
            }

            foreach (var file in Directory.GetFiles(folder.FilePath).OrderBy(Path.GetFileName)) {
                folder.Children.Add(new WebFileItem(file, WebFileItemType.File, folder));
            }
        }

        private void LoadManifestChildren(WebFileItem folder) {
            var relativeFolder = Path.GetRelativePath(ProjectFolder, folder.FilePath).Replace(Path.DirectorySeparatorChar, '/');
            if (relativeFolder == ".") {
                relativeFolder = string.Empty;
            }

            var directItems = _manifestItems!
                .Where(manifestItem => IsDirectChild(relativeFolder, manifestItem.Path))
                .OrderBy(manifestItem => manifestItem.Type == "folder" ? 0 : 1)
                .ThenBy(manifestItem => Path.GetFileName(manifestItem.Path), StringComparer.OrdinalIgnoreCase);

            foreach (var manifestItem in directItems) {
                var path = Path.Combine(ProjectFolder, manifestItem.Path.Replace('/', Path.DirectorySeparatorChar));
                if (manifestItem.Type == "folder") {
                    folder.Children.Add(CreateDirectoryItem(path, folder));
                }
                else if (File.Exists(path)) {
                    folder.Children.Add(new WebFileItem(path, WebFileItemType.File, folder));
                }
            }
        }

        private bool HasChildren(string folderPath) {
            if (_manifestItems != null) {
                var relativeFolder = Path.GetRelativePath(ProjectFolder, folderPath).Replace(Path.DirectorySeparatorChar, '/');
                if (relativeFolder == ".") {
                    relativeFolder = string.Empty;
                }
                return _manifestItems.Any(manifestItem => IsDirectChild(relativeFolder, manifestItem.Path));
            }

            return Directory.EnumerateFileSystemEntries(folderPath).Any();
        }

        private static bool IsDirectChild(string relativeFolder, string itemPath) {
            var directory = Path.GetDirectoryName(itemPath)?.Replace(Path.DirectorySeparatorChar, '/') ?? string.Empty;
            return string.Equals(directory, relativeFolder, StringComparison.OrdinalIgnoreCase);
        }

        private void AddItem(WebFileItem? parent, WebFileItem item) {
            var collection = parent?.Children ?? FileItems;
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
            var collection = oldItem.Parent?.Children ?? FileItems;
            if (collection == null) return;

            var index = collection.IndexOf(oldItem);
            if (index < 0) return;

            collection.RemoveAt(index);
            AddItem(oldItem.Parent, newItem);
        }

        private void RemoveItem(WebFileItem item) {
            var collection = item.Parent?.Children ?? FileItems;
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
                ? new WebFileItem(path, WebFileItemType.Folder, parent)
                : new WebFileItem(path, WebFileItemType.File, parent);
        }

        private static void DeletePath(WebFileItem item) {
            if (item.Type == WebFileItemType.File) {
                File.Delete(item.FilePath);
            }
            else {
                FileUtil.RemoveDirectory(item.FilePath);
            }
        }

        private void ClearClipboard() {
            _clipboardItem = null;
            _clipboardOperation = WebFileTreeClipboardOperation.None;
        }

        private string _projectFolder = string.Empty;
        private const string PlaceholderFileName = ".__lazy_placeholder__";
        private WebDesignFileUtil? _designFileUtil;
        private IReadOnlyList<WebProjectManifestItem>? _manifestItems;
        private WebFileItem? _clipboardItem;
        private WebFileTreeClipboardOperation _clipboardOperation;
    }

    public enum WebFileTreeClipboardOperation {
        None,
        Cut,
        Copy,
    }
}
