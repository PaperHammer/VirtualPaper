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
        // [已废弃，暂注释] 事件从未触发，重命名链路未接
        // public event Action<string>? ProjectFileRenamed;

        public ObservableCollection<WebFileItem> FileItems = [];

        public string ProjectFolder {
            get => _projectFolder;
            private set {
                if (_projectFolder == value) return;
                _projectFolder = value;
                OnPropertyChanged();
            }
        }

        /// <summary>文件树过滤文本（空则不过滤）。</summary>
        public string FilterText {
            get => _filterText;
            set {
                if (_filterText == value) return;
                _filterText = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }

        /// <summary>
        /// 按名称过滤文件树：名称命中过滤词的节点保留；
        /// 存在可见后代节点的目录也保留（保证命中文件的路径可见）。
        /// </summary>
        public void ApplyFilter() {
            var filter = _filterText.Trim();
            foreach (var item in FileItems) {
                ApplyFilterToItem(item, filter);
            }
        }

        private static bool ApplyFilterToItem(WebFileItem item, string filter) {
            if (item.IsPlaceholder) {
                item.IsVisible = false;
                return false;
            }

            var childMatches = false;
            foreach (var child in item.Children) {
                childMatches |= ApplyFilterToItem(child, filter);
            }

            var matches = filter.Length == 0
                || item.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase);
            item.IsVisible = matches || childMatches;
            return item.IsVisible;
        }

        public void Refresh(WebDesignFileUtil designFileUtil) {
            if (!Directory.Exists(designFileUtil.ProjectFolder)) return;

            _designFileUtil = designFileUtil;
            _manifestItems = designFileUtil.GetManifestItems();
            RebuildManifestChildrenIndex();
            ProjectFolder = designFileUtil.ProjectFolder;
            var root = CreateDirectoryItem(ProjectFolder, null);
            LoadChildren(root);
            root.IsExpanded = true;
            FileItems.Clear();
            FileItems.Add(root);
            RebuildPathMap(root);
            ApplyFilter();
        }

        public void Refresh(string projectFolder) {
            if (!Directory.Exists(projectFolder)) return;

            _designFileUtil = null;
            _manifestItems = null;
            _manifestChildren = null;
            ProjectFolder = projectFolder;
            var root = CreateDirectoryItem(projectFolder, null);
            LoadChildren(root);
            root.IsExpanded = true;
            FileItems.Clear();
            FileItems.Add(root);
            RebuildPathMap(root);
            ApplyFilter();
        }

        /// <summary>
        /// 项目清单 (.vpw) 变更时，增量同步文件树
        /// 
        /// 对比新清单与当前文件树，只增删差异项，避免全量刷新导致的闪烁和性能问题
        /// </summary>
        public void SyncManifest(WebDesignFileUtil designFileUtil) {
            if (_designFileUtil == null || _manifestItems == null) {
                Refresh(designFileUtil);
                return;
            }

            _designFileUtil = designFileUtil;
            IReadOnlyList<WebProjectManifestItem> newManifestItems;
            newManifestItems = designFileUtil.GetManifestItems();
            if (ProjectFolder != designFileUtil.ProjectFolder) {
                ProjectFolder = designFileUtil.ProjectFolder;
            }

            // 构建新旧路径集合（绝对路径）
            var newPathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in newManifestItems) {
                newPathSet.Add(Path.Combine(ProjectFolder, item.Path.Replace('/', Path.DirectorySeparatorChar)));
            }

            var oldPathSet = new HashSet<string>(_pathMap.Keys, StringComparer.OrdinalIgnoreCase);

            // 1. 先更新清单引用（后续 HasChildren / CreateDirectoryItem 依赖新清单）
            _manifestItems = newManifestItems;
            RebuildManifestChildrenIndex();

            // 2. 移除不在新清单中的项（按路径深度降序：先删子项再删父项）
            //    排除根目录和占位项
            var removedPaths = oldPathSet.Except(newPathSet)
                .Where(p => !string.Equals(p, ProjectFolder, StringComparison.OrdinalIgnoreCase))
                .ToList();
            removedPaths.Sort((a, b) => b.Count(c => c == Path.DirectorySeparatorChar)
                .CompareTo(a.Count(c => c == Path.DirectorySeparatorChar)));

            foreach (var path in removedPaths) {
                var item = FindItem(path);
                if (item != null && !item.IsPlaceholder) {
                    RemoveItem(item);
                }
            }

            // 3. 添加新清单中有但树中没有的项（按路径深度升序：先加父项再加子项）
            var addedPaths = newPathSet.Except(oldPathSet).ToList();
            addedPaths.Sort((a, b) => a.Count(c => c == Path.DirectorySeparatorChar)
                .CompareTo(b.Count(c => c == Path.DirectorySeparatorChar)));

            var manifestItemMap = new Dictionary<string, WebProjectManifestItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var mi in newManifestItems) {
                manifestItemMap[Path.Combine(ProjectFolder, mi.Path.Replace('/', Path.DirectorySeparatorChar))] = mi;
            }

            foreach (var path in addedPaths) {
                var parentPath = Path.GetDirectoryName(path);
                if (parentPath == null) continue;

                var parent = FindItem(parentPath);
                if (parent == null) continue;

                // 确保父节点的子项已展开加载
                if (!parent.IsChildrenLoaded) {
                    LoadChildren(parent);
                }

                if (!manifestItemMap.TryGetValue(path, out var manifestItem)) continue;

                if (manifestItem.Type == "folder") {
                    AddItem(parent, CreateDirectoryItem(path, parent));
                }
                else {
                    AddItem(parent, new WebFileItem(path, WebFileItemType.File, parent));
                }
            }
            ApplyFilter();
        }

        /// <summary>
        /// 应用 ProjectSystemManager 的变化事件——所有文件系统变更的统一入口
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

                case ProjectChangeType.Modified:
                case ProjectChangeType.Reloaded:
                    // 项目清单 (.vpw) 被修改时，丢弃内存缓存并增量同步文件树
                    if (_designFileUtil?.IsProjectFile(e.Path) == true) {
                        _designFileUtil.ReloadManifest();
                        SyncManifest(_designFileUtil);
                    }
                    break;
            }
        }

        public void SelectFile(string filePath) {
            // Normalize path separators (Monaco sends forward slashes)
            filePath = filePath.Replace('/', Path.DirectorySeparatorChar);

            // Lazily load ancestor folders so the target item is in _pathMap
            EnsureAncestorsLoaded(filePath);

            var item = FindItem(filePath);
            if (item == null) return;

            ExpandParents(item);
        }

        /// <summary>
        /// Walk from project root to the file's parent directory,
        /// loading children of each ancestor folder lazily as needed.
        /// </summary>
        private void EnsureAncestorsLoaded(string filePath) {
            var parentPath = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(parentPath)) return;
            if (!parentPath.StartsWith(ProjectFolder, StringComparison.OrdinalIgnoreCase)) return;

            // Collect all ancestor paths from project folder down to parent
            var ancestors = new List<string>();
            var current = parentPath;
            while (current.Length > ProjectFolder.Length && current.StartsWith(ProjectFolder, StringComparison.OrdinalIgnoreCase)) {
                ancestors.Add(current);
                current = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(current)) break;
            }
            ancestors.Reverse(); // root-first

            foreach (var ancestorPath in ancestors) {
                var folder = FindItem(ancestorPath);
                if (folder != null && !folder.IsChildrenLoaded) {
                    LoadChildren(folder);
                }
            }
        }

        public void SetFileSaved(string filePath, bool isSaved) {
            var item = FindItem(filePath);
            if (item == null) return;

            item.IsSaved = isSaved;
        }

        public void ToggleFolder(WebFileItem folder) {
            if (folder.Type != WebFileItemType.Folder) return;

            // TreeView already toggles IsExpanded before ItemInvoked fires.
            // Only load the lazy children here; toggling it again collapses the
            // folder on its first expansion.
            LoadChildren(folder);
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

            await RenameItemAsync(item, path);
        }

        /// <summary>进入行内重命名模式（名称区切换为输入框）。</summary>
        public void BeginRename(WebFileItem item) {
            CancelRename();

            if (!item.ExistsOnDisk) return;
            if (_designFileUtil?.IsProjectFile(item.FilePath) == true) return;

            _renamingItem = item;
            item.RenameText = item.FileName;
            item.IsRenameInvalid = false;
            item.IsRenaming = true;
        }

        /// <summary>取消行内重命名。</summary>
        public void CancelRename() {
            if (_renamingItem == null) return;
            _renamingItem.IsRenameInvalid = false;
            _renamingItem.IsRenaming = false;
            _renamingItem = null;
        }

        /// <summary>提交行内重命名（Enter/失焦提交，Esc 取消）。</summary>
        public async Task RenameToAsync(WebFileItem item, string newName) {
            if (!item.IsRenaming || _renamingItem != item) return;
            if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, item.FileName, StringComparison.Ordinal)) {
                CancelRename();
                return;
            }

            item.IsRenameInvalid = false;
            item.IsRenaming = false;
            _renamingItem = null;

            var path = FileUtil.NextAvailablePath(Path.Combine(Path.GetDirectoryName(item.FilePath)!, newName.Trim()));
            await RenameItemAsync(item, path);
        }

        private async Task RenameItemAsync(WebFileItem item, string newPath) {
            var oldPath = item.FilePath;
            if (item.Type == WebFileItemType.File) {
                File.Move(oldPath, newPath);
            }
            else {
                Directory.Move(oldPath, newPath);
            }

            _designFileUtil?.RenameManifestPath(oldPath, newPath);
            ReplaceItem(item, BuildItem(newPath, item.Type, item.Parent));
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

        /// <summary>
        /// 外部导入（拖放 / Add Items 菜单）：文件或文件夹复制进目标目录，并登记进 vpw 清单。
        /// </summary>
        public async Task ImportExternalAsync(string sourcePath, string targetFolder) {
            if (Directory.Exists(sourcePath)) {
                var destinationPath = FileUtil.NextAvailablePath(Path.Combine(targetFolder, Path.GetFileName(sourcePath)));
                if (IsPathInManifest(destinationPath)) return;

                // 大目录拷贝放后台线程，避免阻塞 UI
                await Task.Run(() => FileUtil.CopyDirectory(sourcePath, destinationPath, true));
                _designFileUtil?.AddManifestPathRecursive(destinationPath);
                var parent = FindItem(targetFolder);
                AddItem(parent, CreateDirectoryItem(destinationPath, parent));
                return;
            }

            if (!File.Exists(sourcePath)) return;

            var destinationFilePath = FileUtil.NextAvailablePath(Path.Combine(targetFolder, Path.GetFileName(sourcePath)));
            if (IsPathInManifest(destinationFilePath)) return;

            File.Copy(sourcePath, destinationFilePath, overwrite: false);
            _designFileUtil?.AddManifestPath(destinationFilePath);
            var fileParent = FindItem(targetFolder);
            AddItem(fileParent, new WebFileItem(destinationFilePath, WebFileItemType.File, fileParent));
        }

        /// <summary>
        /// 拖拽移动：处理 TreeView 原生拖拽留下的数据变更并真正移动磁盘文件、同步 vpw。
        /// 原生拖拽已把 item 从原父集合移除、追加到落点条目（target）的集合；
        /// 这里按“有效目标目录”重新落位，目标不合法的回退到原始父目录。
        /// </summary>
        public void MoveItemAsync(WebFileItem item, WebFileItem? target, WebFileItem? originalParent) {
            if (item.IsPlaceholder || !item.ExistsOnDisk) return;
            if (_designFileUtil?.IsProjectFile(item.FilePath) == true) return;

            // 落点是文件夹 → 该文件夹；落点是文件 → 其父目录（文件不能当父节点）；null → 项目根目录
            var targetFolder = target?.Type == WebFileItemType.Folder ? target : target?.Parent;
            if (targetFolder == item) return;

            if (item.Type == WebFileItemType.Folder && targetFolder != null && IsDescendantOf(targetFolder, item)) {
                RestoreItemToParent(item, target, originalParent);
                return;
            }

            var destinationFolder = targetFolder?.FilePath ?? ProjectFolder;
            var originalFolderPath = originalParent?.FilePath ?? ProjectFolder;
            var isSameFolder = string.Equals(destinationFolder, originalFolderPath, StringComparison.OrdinalIgnoreCase);
            if (isSameFolder) {
                // 没有真正换目录（或落点不合法）：把条目放回原父目录
                RestoreItemToParent(item, target, originalParent);
                return;
            }

            // 真实移动：磁盘 + vpw 清单 + 数据模型
            var newPath = FileUtil.NextAvailablePath(Path.Combine(destinationFolder, item.FileName));
            var oldPath = item.FilePath;
            if (item.Type == WebFileItemType.File) {
                File.Move(oldPath, newPath);
            }
            else {
                Directory.Move(oldPath, newPath);
            }

            _designFileUtil?.RenameManifestPath(oldPath, newPath);

            RemoveItemFromCollections(item, target, originalParent);
            AddItem(targetFolder, BuildItem(newPath, item.Type, targetFolder));
        }

        /// <summary>
        /// 把被拖条目放回原始父目录：从落点集合与原始集合中移除旧实例，再按原位置重新插入。
        /// </summary>
        private void RestoreItemToParent(WebFileItem item, WebFileItem? dropTarget, WebFileItem? originalParent) {
            RemoveItemFromCollections(item, dropTarget, originalParent);
            AddItem(originalParent, item);
        }

        /// <summary>
        /// 清除旧实例在数据模型中的残留：原生拖拽把条目追加到了落点集合，
        /// 这里从落点集合与原始集合中统一移除，并清理路径索引。
        /// </summary>
        private void RemoveItemFromCollections(WebFileItem item, WebFileItem? dropTarget, WebFileItem? originalParent) {
            dropTarget?.Children.Remove(item);
            (originalParent?.Children ?? FileItems).Remove(item);

            _pathMap.Remove(item.FilePath);
            if (item.Type == WebFileItemType.Folder) {
                RemoveDescendantsFromPathMap(item);
            }
        }

        private static bool IsDescendantOf(WebFileItem item, WebFileItem ancestor) {
            for (var current = item.Parent; current != null; current = current.Parent) {
                if (current == ancestor) return true;
            }
            return false;
        }

        private bool IsPathInManifest(string path) {
            if (_manifestItems == null) return false;

            var relativePath = Path.GetRelativePath(ProjectFolder, path).Replace(Path.DirectorySeparatorChar, '/');
            return _manifestItems.Any(item =>
                string.Equals(item.Path, relativePath, StringComparison.OrdinalIgnoreCase));
        }

        public WebFileItem? FindItem(string filePath) {
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
                var placeholder = new WebFileItem(Path.Combine(folderPath, PlaceholderFileName), WebFileItemType.File, item) { IsPlaceholder = true };
                placeholder.ExistsOnDisk = true;
                placeholder.IsVisible = false;
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

            // 过滤状态下新展开的目录需要重新应用过滤
            if (!string.IsNullOrWhiteSpace(FilterText)) {
                ApplyFilter();
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
            var relativeFolder = GetRelativeFolderPath(folder.FilePath);
            var directItems = _manifestChildren!.TryGetValue(relativeFolder, out var children)
                ? children
                : [];

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
                var relativeFolder = GetRelativeFolderPath(folderPath);
                return _manifestChildren!.ContainsKey(relativeFolder);
            }

            return Directory.EnumerateFileSystemEntries(folderPath).Any();
        }

        private string GetRelativeFolderPath(string folderPath) {
            var relativeFolder = Path.GetRelativePath(ProjectFolder, folderPath).Replace(Path.DirectorySeparatorChar, '/');
            return relativeFolder == "." ? string.Empty : relativeFolder;
        }

        private void RebuildManifestChildrenIndex() {
            if (_manifestItems == null) {
                _manifestChildren = null;
                return;
            }

            _manifestChildren = new Dictionary<string, List<WebProjectManifestItem>>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in _manifestItems) {
                var parentPath = Path.GetDirectoryName(item.Path)?.Replace(Path.DirectorySeparatorChar, '/') ?? string.Empty;
                if (!_manifestChildren.TryGetValue(parentPath, out var children)) {
                    children = [];
                    _manifestChildren[parentPath] = children;
                }
                children.Add(item);
            }
            foreach (var children in _manifestChildren.Values) {
                children.Sort(CompareManifestItems);
            }
        }

        private static int CompareManifestItems(WebProjectManifestItem left, WebProjectManifestItem right) {
            var typeOrder = string.Equals(left.Type, "folder", StringComparison.Ordinal) ? 0 : 1;
            var otherTypeOrder = string.Equals(right.Type, "folder", StringComparison.Ordinal) ? 0 : 1;
            if (typeOrder != otherTypeOrder) return typeOrder - otherTypeOrder;
            return string.Compare(Path.GetFileName(left.Path), Path.GetFileName(right.Path), StringComparison.OrdinalIgnoreCase);
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
        private string _filterText = string.Empty;
        private const string PlaceholderFileName = ".__lazy_placeholder__";
        private readonly Dictionary<string, WebFileItem> _pathMap = new(StringComparer.OrdinalIgnoreCase);
        private WebDesignFileUtil? _designFileUtil;
        private IReadOnlyList<WebProjectManifestItem>? _manifestItems;
        private Dictionary<string, List<WebProjectManifestItem>>? _manifestChildren;
        private WebFileItem? _clipboardItem;
        private WebFileTreeClipboardOperation _clipboardOperation;
        private WebFileItem? _renamingItem;
    }

    public enum WebFileTreeClipboardOperation {
        None,
        Cut,
        Copy,
    }
}
