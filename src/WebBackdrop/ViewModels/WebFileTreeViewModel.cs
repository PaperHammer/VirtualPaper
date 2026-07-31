using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.ProjectSystem.Events;
using VirtualPaper.Models.Mvvm;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.Models.SerializableData;

namespace Workloads.Creation.WebBackdrop.ViewModels {
    public partial class WebFileTreeViewModel : ObservableObject {
        public event Action<string>? ProjectFileRenamed;

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
            RebuildPathMap(root);
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
            RebuildPathMap(root);
        }

        /// <summary>
        /// 应用 ProjectSystemManager 的变化事件
        /// 
        /// 增量更新文件树
        /// 不需要重新扫描整个目录
        /// </summary>
        public void ApplyChange(ProjectChangedEvent e) {
            switch (e.Type) {
                case ProjectChangeType.Created:
                    ApplyCreated(e.Path);
                    break;

                case ProjectChangeType.Deleted:
                    ApplyDeleted(e.Path);
                    break;

                case ProjectChangeType.Renamed:
                    ApplyRenamed(e.OldPath!, e.Path);
                    break;
            }
        }

        public void SelectFile(string filePath) {
            var item = FindItem(filePath);
            if (item == null) return;

            ExpandParents(item);
        }

        public void SetFileSaved(string filePath, bool isSaved) {
            var item = FindItem(filePath);
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

        public bool IsProjectFileItem(WebFileItem item) {
            return _designFileUtil?.IsProjectFile(item.FilePath) == true;
        }

        public async Task CreateFileAsync(string folderPath, Func<string, Task<string?>> getRenamedPathAsync) {
            var path = await getRenamedPathAsync(Path.Combine(folderPath, "New File.txt"));
            if (path == null) return;

            if (IsPathInManifest(path)) return;
            if (_designFileUtil?.IsProjectFile(path) == true) return;

            File.WriteAllText(path, string.Empty);
            _designFileUtil?.AddManifestPath(path);
            var parent = FindItem(folderPath);
            AddItem(parent, new WebFileItem(path, WebFileItemType.File, parent));
        }

        public async Task CreateFolderAsync(string folderPath, Func<string, Task<string?>> getRenamedPathAsync) {
            var path = await getRenamedPathAsync(Path.Combine(folderPath, "New Folder"));
            if (path == null) return;

            if (IsPathInManifest(path)) return;
            if (_designFileUtil?.IsProjectFile(path) == true) return;

            Directory.CreateDirectory(path);
            _designFileUtil?.AddManifestPath(path);
            var parent = FindItem(folderPath);
            AddItem(parent, CreateDirectoryItem(path, parent));
        }

        public async Task RenameAsync(WebFileItem item, Func<string, Task<string?>> getRenamedPathAsync) {
            if (!item.ExistsOnDisk) return;
            if (_designFileUtil?.IsProjectFile(item.FilePath) == true) return;

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
            if (!item.ExistsOnDisk) return;
            if (_designFileUtil?.IsProjectFile(item.FilePath) == true) return;

            _clipboardItem = item;
            _clipboardOperation = WebFileTreeClipboardOperation.Cut;
        }

        public void Copy(WebFileItem item) {
            if (!item.ExistsOnDisk && item.Type == WebFileItemType.Folder) return;
            if (_designFileUtil?.IsProjectFile(item.FilePath) == true) return;

            _clipboardItem = item;
            _clipboardOperation = WebFileTreeClipboardOperation.Copy;
        }

        public void PasteTo(WebFileItem target) {
            if (!CanPasteTo(target)) return;
            if (!target.ExistsOnDisk) return;

            var source = _clipboardItem!;
            var destinationPath = FileUtil.NextAvailablePath(Path.Combine(target.FilePath, source.FileName));
            if (_designFileUtil?.IsProjectFile(destinationPath) == true) return;

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
            if (_designFileUtil?.IsProjectFile(item.FilePath) == true) return;

            _designFileUtil?.RemoveManifestPath(item.FilePath);
            if (item.ExistsOnDisk) {
                DeletePath(item);
            }
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

        public async Task ImportExternalFileAsync(string sourcePath, string targetFolder) {
            var destinationPath = FileUtil.NextAvailablePath(Path.Combine(targetFolder, Path.GetFileName(sourcePath)));
            if (IsPathInManifest(destinationPath)) return;

            File.Copy(sourcePath, destinationPath, overwrite: false);
            _designFileUtil?.AddManifestPath(destinationPath);
            var parent = FindItem(targetFolder);
            AddItem(parent, new WebFileItem(destinationPath, WebFileItemType.File, parent));
        }

        private bool IsPathInManifest(string path) {
            if (_manifestItems == null) return false;

            var relativePath = Path.GetRelativePath(ProjectFolder, path).Replace(Path.DirectorySeparatorChar, '/');
            return _manifestItems.Any(item =>
                string.Equals(item.Path, relativePath, StringComparison.OrdinalIgnoreCase));
        }

        private WebFileItem? FindItem(string filePath) {
            return _pathMap.TryGetValue(filePath, out var item) ? item : null;
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
                var placeholder = new WebFileItem(Path.Combine(folderPath, PlaceholderFileName), WebFileItemType.File, item)
                    { IsPlaceholder = true };
                placeholder.ExistsOnDisk = true;
                item.Children.Add(placeholder);
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

            foreach (var child in folder.Children) {
                _pathMap[child.FilePath] = child;
            }
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
                else {
                    var fileItem = new WebFileItem(path, WebFileItemType.File, folder);
                    folder.Children.Add(fileItem);
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

            // 防止文件系统监控器触发的重复添加
            if (_pathMap.ContainsKey(item.FilePath)) return;

            var index = 0;
            while (index < collection.Count && CompareFileItems(collection[index], item) <= 0) {
                index++;
            }
            collection.Insert(index, item);
            _pathMap[item.FilePath] = item;
            if (parent != null) {
                parent.IsExpanded = true;
            }
        }

        private void ReplaceItem(WebFileItem oldItem, WebFileItem newItem) {
            var collection = oldItem.Parent?.Children ?? FileItems;
            if (collection == null) return;

            var index = collection.IndexOf(oldItem);
            if (index < 0) return;

            _pathMap.Remove(oldItem.FilePath);
            collection.RemoveAt(index);
            AddItem(oldItem.Parent, newItem);
        }

        private void RemoveItem(WebFileItem item) {
            _pathMap.Remove(item.FilePath);
            RemoveDescendantsFromPathMap(item);

            var collection = item.Parent?.Children ?? FileItems;
            collection?.Remove(item);
        }

        private void RemoveDescendantsFromPathMap(WebFileItem item) {
            foreach (var child in item.Children) {
                _pathMap.Remove(child.FilePath);
                RemoveDescendantsFromPathMap(child);
            }
        }

        private void RebuildPathMap(WebFileItem root) {
            _pathMap.Clear();
            AddToPathMap(root);
        }

        private void AddToPathMap(WebFileItem item) {
            _pathMap[item.FilePath] = item;
            foreach (var child in item.Children) {
                AddToPathMap(child);
            }
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

        private void ApplyCreated(string path) {
            var parentPath = Path.GetDirectoryName(path);
            if (parentPath == null) return;

            var parent = FindItem(parentPath);
            if (parent == null) return;

            if (Directory.Exists(path)) {
                var folderItem = CreateDirectoryItem(path, parent);
                AddItem(parent, folderItem);
            }
            else {
                var fileItem = new WebFileItem(path, WebFileItemType.File, parent);
                AddItem(parent, fileItem);
            }
        }

        private void ApplyDeleted(string path) {
            var item = FindItem(path);
            if (item == null) return;

            RemoveItem(item);
        }

        private void ApplyRenamed(string oldPath, string newPath) {
            var oldItem = FindItem(oldPath);
            if (oldItem == null) return;

            var newItem = BuildItem(newPath, oldItem.Type, oldItem.Parent);
            ReplaceItem(oldItem, newItem);
        }

        private string _projectFolder = string.Empty;
        private const string PlaceholderFileName = ".__lazy_placeholder__";
        private readonly Dictionary<string, WebFileItem> _pathMap = new(StringComparer.OrdinalIgnoreCase);
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
