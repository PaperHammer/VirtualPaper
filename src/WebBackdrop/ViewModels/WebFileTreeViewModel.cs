using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.ProjectSystem.Events;
using VirtualPaper.Models.Mvvm;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.Models.SerializableData;

namespace Workloads.Creation.WebBackdrop.ViewModels {
    public partial class WebFileTreeViewModel : ObservableObject {
        public ObservableCollection<WebFileItem> FileItems { get; } = [];
        public ObservableCollection<WebContentSearchFileResult> SearchResults { get; } = [];

        public string ProjectFolder {
            get => _projectFolder;
            private set {
                if (_projectFolder == value) return;
                _projectFolder = value;
                OnPropertyChanged();
            }
        }

        /// <summary>项目全文搜索文本。</summary>
        public string FilterText {
            get => _filterText;
            set {
                if (_filterText == value) return;
                _filterText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSearchMode));

                SearchResults.Clear();
                // 防抖后搜索，避免每个按键都读取项目文件
                _filterDebounce?.Cancel();
                _filterDebounce = new CancellationTokenSource();
                IsSearching = !string.IsNullOrWhiteSpace(value);
                _ = ApplyFilterDebouncedAsync(_filterDebounce.Token);
            }
        }

        public bool IsSearchMode => !string.IsNullOrWhiteSpace(FilterText);

        public bool IsCaseSensitive {
            get => _isCaseSensitive;
            set {
                if (_isCaseSensitive == value) return;
                _isCaseSensitive = value;
                OnPropertyChanged();
                RestartSearch();
            }
        }

        public bool IsWholeWord {
            get => _isWholeWord;
            set {
                if (_isWholeWord == value) return;
                _isWholeWord = value;
                OnPropertyChanged();
                RestartSearch();
            }
        }

        private void RestartSearch() {
            if (!IsSearchMode) return;

            SearchResults.Clear();
            _filterDebounce?.Cancel();
            _filterDebounce = new CancellationTokenSource();
            IsSearching = true;
            _ = ApplyFilterDebouncedAsync(_filterDebounce.Token);
        }

        /// <summary>是否正在后台搜索文件内容。</summary>
        public bool IsSearching {
            get => _isSearching;
            private set {
                if (_isSearching == value) return;
                _isSearching = value;
                OnPropertyChanged();
            }
        }

        private async Task ApplyFilterDebouncedAsync(CancellationToken token) {
            //延时不用 token（取消时 Task.Delay(token) 会抛调试器可见的取消异常噪音）    
            await Task.Delay(200);
            if (token.IsCancellationRequested) return;

            var filter = _filterText.Trim();
            if (filter.Length == 0) {
                if (!token.IsCancellationRequested) IsSearching = false;
                return;
            }

            try {
                var isCaseSensitive = IsCaseSensitive;
                var isWholeWord = IsWholeWord;
                // 文件读取放到线程池；搜索结果集合仍只在 UI 线程更新。
                var results = await Task.Run(
                    () => FindContentMatches(filter, isCaseSensitive, isWholeWord, token),
                    token);
                token.ThrowIfCancellationRequested();

                SearchResults.Clear();
                for (var resultIndex = 0; resultIndex < results.Count; resultIndex++) {
                    var result = results[resultIndex];
                    var fileResult = new WebContentSearchFileResult {
                        FilePath = result.FilePath,
                        RelativePath = Path.GetRelativePath(ProjectFolder, result.FilePath),
                    };
                    foreach (var match in result.Matches) {
                        fileResult.Matches.Add(new WebContentSearchMatch {
                            FilePath = result.FilePath,
                            LineNumber = match.LineNumber,
                            ColumnNumber = match.ColumnNumber,
                            PreviewText = match.PreviewText,
                        });
                    }
                    SearchResults.Add(fileResult);

                    // 分批回填，避免大量命中文件一次性占满 UI 线程。
                    if ((resultIndex + 1) % SearchResultUiBatchSize == 0) {
                        await Task.Yield();
                        token.ThrowIfCancellationRequested();
                    }
                }
            }
            catch (OperationCanceledException) {
                // 新的搜索词会取消本次扫描。
            }
            finally {
                if (!token.IsCancellationRequested) IsSearching = false;
            }
        }

        private List<ContentSearchFileData> FindContentMatches(
            string filter,
            bool isCaseSensitive,
            bool isWholeWord,
            CancellationToken token) {
            var results = new List<ContentSearchFileData>();
            var paths = GetSearchableFilePaths(token);
            var totalMatches = 0;
            var comparison = isCaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            foreach (var path in paths) {
                token.ThrowIfCancellationRequested();
                try {
                    var fileInfo = new FileInfo(path);
                    if (!fileInfo.Exists || fileInfo.Length > MaxContentSearchFileSize) continue;

                    var fileMatches = new List<ContentSearchMatchData>();
                    var lineNumber = 0;
                    foreach (var line in File.ReadLines(path)) {
                        token.ThrowIfCancellationRequested();
                        lineNumber++;
                        var column = FindMatchColumn(line, filter, comparison, isWholeWord);
                        if (column < 0) continue;

                        fileMatches.Add(new ContentSearchMatchData(
                            lineNumber,
                            column + 1,
                            CreateMatchPreview(line, column, filter.Length)));
                        totalMatches++;
                        if (totalMatches >= MaxContentSearchMatches) break;
                    }

                    if (fileMatches.Count > 0) {
                        results.Add(new ContentSearchFileData(path, fileMatches));
                    }
                    if (totalMatches >= MaxContentSearchMatches) {
                        break;
                    }
                }
                catch (UnauthorizedAccessException) {
                    // 跳过无法读取的文件。
                }
                catch (IOException) {
                    // 文件可能在搜索过程中被移动或删除。
                }
            }

            return results;
        }

        private static int FindMatchColumn(
            string line,
            string filter,
            StringComparison comparison,
            bool isWholeWord) {
            var searchStart = 0;
            while (searchStart <= line.Length - filter.Length) {
                var column = line.IndexOf(filter, searchStart, comparison);
                if (column < 0) return -1;
                if (!isWholeWord || IsWholeWordMatch(line, column, filter.Length)) return column;
                searchStart = column + 1;
            }
            return -1;
        }

        private static bool IsWholeWordMatch(string line, int start, int length) {
            var hasWordCharacterBefore = start > 0 && IsWordCharacter(line[start - 1]);
            var end = start + length;
            var hasWordCharacterAfter = end < line.Length && IsWordCharacter(line[end]);
            return !hasWordCharacterBefore && !hasWordCharacterAfter;
        }

        private static bool IsWordCharacter(char value) => char.IsLetterOrDigit(value) || value == '_';

        private static string CreateMatchPreview(string line, int matchStart, int matchLength) {
            var trimmedStart = 0;
            while (trimmedStart < line.Length && char.IsWhiteSpace(line[trimmedStart])) {
                trimmedStart++;
            }

            if (line.Length - trimmedStart <= MaxContentSearchPreviewLength) {
                return line[trimmedStart..];
            }

            var contextStart = Math.Max(trimmedStart, matchStart - ContentSearchPreviewContextLength);
            var contextEnd = Math.Min(
                line.Length,
                Math.Max(matchStart + matchLength + ContentSearchPreviewContextLength,
                    contextStart + MaxContentSearchPreviewLength));
            contextEnd = Math.Min(contextEnd, contextStart + MaxContentSearchPreviewLength);

            var prefix = contextStart > trimmedStart ? "…" : string.Empty;
            var suffix = contextEnd < line.Length ? "…" : string.Empty;
            return prefix + line[contextStart..contextEnd] + suffix;
        }

        private IEnumerable<string> GetSearchableFilePaths(CancellationToken token) =>
            WebProjectFileEnumerator
                .EnumerateFiles(ProjectFolder, token)
                .Where(path => WebEditorFileUtil.IsTextExtension(Path.GetExtension(path)));

        private sealed record ContentSearchFileData(
            string FilePath,
            List<ContentSearchMatchData> Matches);

        private sealed record ContentSearchMatchData(
            int LineNumber,
            int ColumnNumber,
            string PreviewText);

        // Refresh

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
        }

        // Manifest

        /// <summary>项目清单发生变化时增量同步文件树。</summary>
        public void SyncManifest(WebDesignFileUtil designFileUtil) {
            if (_designFileUtil == null || _manifestItems == null) {
                Refresh(designFileUtil);
                return;
            }

            _designFileUtil = designFileUtil;

            var newManifestItems = designFileUtil.GetManifestItems();
            ProjectFolder = designFileUtil.ProjectFolder;

            var oldManifestItems = _manifestItems;

            var oldPathSet = new HashSet<string>(
                oldManifestItems.Select(GetManifestAbsolutePath),
                StringComparer.OrdinalIgnoreCase);
            var newPathSet = new HashSet<string>(
                newManifestItems.Select(GetManifestAbsolutePath),
                StringComparer.OrdinalIgnoreCase);

            // 先更新 manifest。
            _manifestItems = newManifestItems;
            RebuildManifestChildrenIndex();

            var removedPaths = oldPathSet
                .Except(newPathSet)
                .Where(path => !string.Equals(path, ProjectFolder, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(GetPathDepth)
                .ToList();

            foreach (var path in removedPaths) {
                var item = FindItem(path);
                if (item != null && !item.IsPlaceholder) {
                    RemoveItem(item);
                }
            }

            var addedPaths = newPathSet
                .Except(oldPathSet)
                .OrderBy(GetPathDepth)
                .ToList();

            var manifestItemMap = new Dictionary<string, WebProjectManifestItem>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var manifestItem in newManifestItems) {
                manifestItemMap[GetManifestAbsolutePath(manifestItem)] = manifestItem;
            }

            foreach (var path in addedPaths) {
                var parentPath = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(parentPath)) continue;

                var parent = FindItem(parentPath);
                if (parent == null || parent.Type != WebFileItemType.Folder) continue;

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

        }

        private string GetManifestAbsolutePath(WebProjectManifestItem item) {
            return Path.Combine(ProjectFolder, item.Path.Replace('/', Path.DirectorySeparatorChar));
        }

        private static int GetPathDepth(string path) {
            return path.Count(c => c == Path.DirectorySeparatorChar);
        }

        private void RefreshManifestSnapshot() {
            if (_designFileUtil == null) return;
            _manifestItems = _designFileUtil.GetManifestItems();
            RebuildManifestChildrenIndex();
        }

        // Project change

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
                    if (_designFileUtil?.IsProjectFile(e.Path) == true) {
                        _designFileUtil.ReloadManifest();
                        SyncManifest(_designFileUtil);
                    }
                    break;
            }
        }

        // Selection

        public void SelectFile(string filePath) {
            var item = EnsurePathLoaded(filePath.Replace('/', Path.DirectorySeparatorChar));
            if (item == null) return;

            ExpandParents(item);
        }

        private WebFileItem? EnsurePathLoaded(string filePath) {
            var fullPath = Path.GetFullPath(filePath);
            if (!IsPathInsideProject(fullPath)) return null;

            var root = FindItem(ProjectFolder);
            if (root == null) return null;
            if (string.Equals(fullPath, Path.GetFullPath(ProjectFolder), StringComparison.OrdinalIgnoreCase)) {
                return root;
            }

            var relativePath = Path.GetRelativePath(ProjectFolder, fullPath);
            var segments = relativePath.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            var currentPath = Path.GetFullPath(ProjectFolder);

            for (var index = 0; index < segments.Length; index++) {
                if (current.Type != WebFileItemType.Folder) return null;
                EnsureChildrenLoaded(current);

                currentPath = Path.Combine(currentPath, segments[index]);
                var child = current.Children.FirstOrDefault(item =>
                    !item.IsPlaceholder
                    && string.Equals(item.FilePath, currentPath, StringComparison.OrdinalIgnoreCase));

                if (child == null) {
                    // 搜索器直接扫描磁盘，目标可能尚未进入 manifest/懒加载树。
                    // 这里只补齐目标路径链，不枚举或常驻整个目录，控制内存开销。
                    if (Directory.Exists(currentPath)) {
                        child = CreateDirectoryItem(currentPath, current);
                    }
                    else if (index == segments.Length - 1 && File.Exists(currentPath)) {
                        child = new WebFileItem(currentPath, WebFileItemType.File, current);
                    }
                    else {
                        return null;
                    }

                    AddItem(current, child);
                }

                current = child;
            }

            return current;
        }

        private bool IsPathInsideProject(string path) {
            var fullProject = Path.GetFullPath(ProjectFolder).TrimEnd(Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

            return string.Equals(fullPath, fullProject, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(
                    fullProject + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase);
        }

        // Basic operations

        public void SetFileSaved(string filePath, bool isSaved) {
            var item = FindItem(filePath);
            if (item == null) return;
            item.IsSaved = isSaved;
        }

        public void ToggleFolder(WebFileItem folder) {
            if (folder.Type != WebFileItemType.Folder) return;
            LoadChildren(folder);
        }

        public bool IsProjectFileItem(WebFileItem item) {
            return _designFileUtil?.IsProjectFile(item.FilePath) == true;
        }

        // Create

        public async Task CreateFileAsync(string folderPath, Func<string, Task<string?>> getRenamedPathAsync) {
            var path = await getRenamedPathAsync(Path.Combine(folderPath, "New File.txt"));
            if (path == null) return;
            if (IsPathInManifest(path)) return;
            if (_designFileUtil?.IsProjectFile(path) == true) return;

            File.WriteAllText(path, string.Empty);

            _designFileUtil?.AddManifestPath(path);
            RefreshManifestSnapshot();

            var parent = FindItem(folderPath);
            if (parent == null) return;

            EnsureChildrenLoaded(parent);
            AddItem(parent, new WebFileItem(path, WebFileItemType.File, parent));
        }

        public async Task CreateFolderAsync(string folderPath, Func<string, Task<string?>> getRenamedPathAsync) {
            var path = await getRenamedPathAsync(Path.Combine(folderPath, "New Folder"));
            if (path == null) return;
            if (IsPathInManifest(path)) return;
            if (_designFileUtil?.IsProjectFile(path) == true) return;

            Directory.CreateDirectory(path);

            _designFileUtil?.AddManifestPath(path);
            RefreshManifestSnapshot();

            var parent = FindItem(folderPath);
            if (parent == null) return;

            EnsureChildrenLoaded(parent);
            AddItem(parent, CreateDirectoryItem(path, parent));
        }

        // Rename

        public async Task RenameAsync(WebFileItem item, Func<string, Task<string?>> getRenamedPathAsync) {
            if (!item.ExistsOnDisk) return;
            if (_designFileUtil?.IsProjectFile(item.FilePath) == true) return;

            var path = await getRenamedPathAsync(item.FilePath);
            if (path == null) return;

            await RenameItemAsync(item, path);
        }

        public void BeginRename(WebFileItem item) {
            CancelRename();

            if (!item.ExistsOnDisk) return;
            if (_designFileUtil?.IsProjectFile(item.FilePath) == true) return;

            _renamingItem = item;
            item.RenameText = item.FileName;
            item.IsRenameInvalid = false;
            item.IsRenaming = true;
        }

        public void CancelRename() {
            if (_renamingItem == null) return;

            _renamingItem.IsRenameInvalid = false;
            _renamingItem.IsRenaming = false;
            _renamingItem = null;
        }

        public async Task RenameToAsync(WebFileItem item, string newName) {
            if (!item.IsRenaming || !ReferenceEquals(_renamingItem, item)) return;

            if (string.IsNullOrWhiteSpace(newName)
                || string.Equals(newName, item.FileName, StringComparison.Ordinal)) {
                CancelRename();
                return;
            }

            item.IsRenameInvalid = false;
            item.IsRenaming = false;
            _renamingItem = null;

            var parentPath = Path.GetDirectoryName(item.FilePath);
            if (string.IsNullOrEmpty(parentPath)) return;

            var path = FileUtil.NextAvailablePath(Path.Combine(parentPath, newName.Trim()));
            await RenameItemAsync(item, path);
        }

        private async Task RenameItemAsync(WebFileItem item, string newPath) {
            var oldPath = item.FilePath;

            if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) return;

            try {
                if (item.Type == WebFileItemType.File) {
                    File.Move(oldPath, newPath);
                }
                else {
                    Directory.Move(oldPath, newPath);
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WebFileTreeViewModel>()
                    .Error($"Failed to rename: {oldPath} -> {newPath}", ex);
                return;
            }

            _designFileUtil?.RenameManifestPath(oldPath, newPath);
            RefreshManifestSnapshot();

            RebindItemPath(item, newPath, item.Parent);
            SortItemInParent(item);

            await Task.CompletedTask;
        }

        // Cut / Copy / Paste

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

            try {
                if (source.Type == WebFileItemType.File) {
                    File.Copy(source.FilePath, destinationPath);
                }
                else if (_clipboardOperation == WebFileTreeClipboardOperation.Cut) {
                    Directory.Move(source.FilePath, destinationPath);
                }
                else {
                    FileUtil.CopyDirectory(source.FilePath, destinationPath, true);
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WebFileTreeViewModel>()
                    .Error($"Failed to paste: {source.FilePath} -> {destinationPath}", ex);
                return;
            }

            if (_clipboardOperation == WebFileTreeClipboardOperation.Cut) {
                _designFileUtil?.RenameManifestPath(source.FilePath, destinationPath);
                RefreshManifestSnapshot();

                var oldParent = source.Parent;
                RebindItemPath(source, destinationPath, target);
                oldParent?.Children.Remove(source);

                EnsureChildrenLoaded(target);
                AddItem(target, source);

                ClearClipboard();
            }
            else {
                _designFileUtil?.AddManifestPathRecursive(destinationPath);
                RefreshManifestSnapshot();

                EnsureChildrenLoaded(target);
                AddItem(target, BuildItem(destinationPath, source.Type, target));
            }

            target.IsExpanded = true;
        }

        public void Delete(WebFileItem item) {
            if (_designFileUtil?.IsProjectFile(item.FilePath) == true) return;

            _designFileUtil?.RemoveManifestPath(item.FilePath);

            if (item.ExistsOnDisk) {
                try {
                    DeletePath(item);
                }
                catch (Exception ex) {
                    ArcLog.GetLogger<WebFileTreeViewModel>()
                        .Error($"Failed to delete: {item.FilePath}", ex);
                    return;
                }
            }

            RemoveItem(item);

            if (ReferenceEquals(_clipboardItem, item)) {
                ClearClipboard();
            }

            RefreshManifestSnapshot();
        }

        public bool CanPasteTo(WebFileItem target) {
            if (_clipboardItem == null
                || _clipboardOperation == WebFileTreeClipboardOperation.None) {
                return false;
            }

            if (target.Type != WebFileItemType.Folder) return false;

            if (!File.Exists(_clipboardItem.FilePath)
                && !Directory.Exists(_clipboardItem.FilePath)) {
                return false;
            }

            if (_clipboardOperation == WebFileTreeClipboardOperation.Copy) return true;

            if (string.Equals(_clipboardItem.FilePath, target.FilePath, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            if (_clipboardItem.Type == WebFileItemType.Folder) {
                var relativePath = Path.GetRelativePath(_clipboardItem.FilePath, target.FilePath);
                if (relativePath == "."
                    || (!relativePath.StartsWith("..") && !Path.IsPathRooted(relativePath))) {
                    return false;
                }
            }

            return true;
        }

        // External import

        public async Task ImportExternalAsync(string sourcePath, string targetFolder) {
            var parent = FindItem(targetFolder);
            if (parent == null || parent.Type != WebFileItemType.Folder) return;

            EnsureChildrenLoaded(parent);

            if (Directory.Exists(sourcePath)) {
                var destinationPath = FileUtil.NextAvailablePath(
                    Path.Combine(targetFolder, Path.GetFileName(sourcePath)));
                if (IsPathInManifest(destinationPath)) return;

                // 大目录拷贝放后台线程，避免阻塞 UI
                await Task.Run(() => FileUtil.CopyDirectory(sourcePath, destinationPath, true));

                _designFileUtil?.AddManifestPathRecursive(destinationPath);
                RefreshManifestSnapshot();

                AddItem(parent, CreateDirectoryItem(destinationPath, parent));
                return;
            }

            if (!File.Exists(sourcePath)) return;

            var destinationFilePath = FileUtil.NextAvailablePath(
                Path.Combine(targetFolder, Path.GetFileName(sourcePath)));
            if (IsPathInManifest(destinationFilePath)) return;

            File.Copy(sourcePath, destinationFilePath, overwrite: false);

            _designFileUtil?.AddManifestPath(destinationFilePath);
            RefreshManifestSnapshot();

            AddItem(parent, new WebFileItem(destinationFilePath, WebFileItemType.File, parent));
        }

        // Move

        /// <summary>
        /// 多选移动：原生拖拽已把条目从原集合移除、追加到落点集合（节点向量会回写数据源），
        /// 这里只做数据层最终落位：跨目录移动（磁盘 + manifest + 路径重绑）或同目录重新排序。
        /// 集合变更事件会驱动 TreeView 自动重建对应节点。
        /// </summary>
        public IReadOnlyList<WebFileItem> MoveItems(IEnumerable<WebFileItem> items, WebFileItem? target) {
            var result = new List<WebFileItem>();

            var targetFolder = ResolveDropTargetFolder(target);
            if (targetFolder != null) {
                EnsureChildrenLoaded(targetFolder);
            }

            var allItems = items.Distinct().ToList();
            var validItems = allItems
                .Where(item => CanMove(item, targetFolder))
                .ToList();
            validItems.Sort((a, b) => string.Compare(a.FilePath, b.FilePath, StringComparison.OrdinalIgnoreCase));

            foreach (var item in validItems) {
                // 原生拖拽不改 item 对象，Parent 仍是原父级
                var originalParent = item.Parent;
                var sourceFolderPath = originalParent?.FilePath ?? ProjectFolder;
                var destinationFolderPath = targetFolder?.FilePath ?? ProjectFolder;
                var isSameFolder = string.Equals(sourceFolderPath, destinationFolderPath, StringComparison.OrdinalIgnoreCase);

                // 跨目录：真实移动（磁盘 + manifest + 路径重绑）
                if (!isSameFolder) {
                    var oldPath = item.FilePath;
                    var newPath = FileUtil.NextAvailablePath(Path.Combine(destinationFolderPath, item.FileName));
                    if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) {
                        if (!MoveOnDisk(item, oldPath, newPath)) continue;
                        _designFileUtil?.RenameManifestPath(oldPath, newPath);
                        RebindItemPath(item, newPath, targetFolder);
                    }
                }

                // 数据落位：从落点集合与原集合移除（原生已把条目追加到落点集合），
                // 再按排序插入目标集合；同目录时这一步实现“重新排序”。
                target?.Children.Remove(item);
                originalParent?.Children.Remove(item);
                AddItem(targetFolder, item);

                result.Add(item);
            }

            // 不可移动的条目（项目文件/占位/磁盘缺失/自拖/拖入子孙目录）：
            // 原生拖拽可能已把它们挂到落点集合（甚至文件条目下），
            // 统一放回原父集合，避免被挂载到文件下或遗留在非法位置。
            foreach (var item in allItems) {
                if (validItems.Contains(item)) continue;

                target?.Children.Remove(item);
                AddItem(item.Parent, item);
            }

            RefreshManifestSnapshot();

            if (targetFolder != null) {
                targetFolder.IsExpanded = true;
            }

            return result;
        }

        private bool CanMove(WebFileItem item, WebFileItem? targetFolder) {
            // 占位节点不能移动
            if (item.IsPlaceholder) return false;
            // 磁盘上不存在的节点不能移动
            if (!item.ExistsOnDisk) return false;
            // 项目文件不能移动
            if (_designFileUtil?.IsProjectFile(item.FilePath) == true) return false;
            // 不能拖入自身
            if (ReferenceEquals(item, targetFolder)) return false;
            // 不能拖入自身的后代目录
            if (item.Type == WebFileItemType.Folder
                && targetFolder != null
                && IsDescendantOf(targetFolder, item)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 源头校验：条目是否允许被拖拽（项目文件/占位/磁盘缺失不可拖拽）。
        /// 供视图在 DragItemsStarting 里取消拖拽，避免完成后再回退。
        /// </summary>
        public bool CanDragItem(WebFileItem item) => CanMove(item, null);

        private static bool MoveOnDisk(WebFileItem item, string oldPath, string newPath) {
            try {
                if (item.Type == WebFileItemType.File) {
                    File.Move(oldPath, newPath);
                }
                else {
                    Directory.Move(oldPath, newPath);
                }

                return true;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WebFileTreeViewModel>().Error($"Failed to move file: {oldPath} -> {newPath}", ex);
                return false;
            }
        }

        // Rebind

        /// <summary>
        /// 修改 WebFileItem 的路径和 Parent。
        /// 对文件夹：同时递归修改已经加载到内存中的所有后代节点。
        /// 对未加载的目录：不需要提前创建子节点，后续 LoadChildren 会根据最新 manifest 加载。
        /// 这是新的文件树模型中最核心的路径同步入口。
        /// </summary>
        private void RebindItemPath(WebFileItem item, string newPath, WebFileItem? newParent) {
            var oldPath = item.FilePath;

            item.RebindLocation(newPath, newParent);

            if (item.Type != WebFileItemType.Folder) return;

            RebindLoadedDescendants(item, oldPath, newPath);
        }

        private void RebindLoadedDescendants(WebFileItem folder, string oldFolderPath, string newFolderPath) {
            foreach (var child in folder.Children) {
                if (child.IsPlaceholder) {
                    child.RebindLocation(Path.Combine(newFolderPath, PlaceholderFileName), folder);
                    continue;
                }

                var relativePath = Path.GetRelativePath(oldFolderPath, child.FilePath);
                var newChildPath = Path.Combine(newFolderPath, relativePath);

                child.RebindLocation(newChildPath, folder);

                if (child.Type == WebFileItemType.Folder) {
                    RebindLoadedDescendants(child, Path.Combine(oldFolderPath, relativePath), newChildPath);
                }
            }
        }

        // Tree operations

        private WebFileItem? ResolveDropTargetFolder(WebFileItem? target) {
            if (target == null) return null;

            return target.Type == WebFileItemType.Folder
                ? target
                : target.Parent;
        }

        private void EnsureChildrenLoaded(WebFileItem? folder) {
            if (folder == null || folder.Type != WebFileItemType.Folder) return;

            if (!folder.IsChildrenLoaded) {
                LoadChildren(folder);
            }
        }

        private static bool IsDescendantOf(WebFileItem item, WebFileItem ancestor) {
            for (var current = item.Parent; current != null; current = current.Parent) {
                if (ReferenceEquals(current, ancestor)) return true;
            }

            return false;
        }

        /// <summary>
        /// 根据树结构寻找节点（不再维护独立 _pathMap）。
        /// </summary>
        public WebFileItem? FindItem(string filePath) {
            if (string.IsNullOrEmpty(filePath)) return null;

            filePath = Path.GetFullPath(filePath);

            foreach (var root in FileItems) {
                var found = FindItemRecursive(root, filePath);
                if (found != null) return found;
            }

            return null;
        }

        private static WebFileItem? FindItemRecursive(WebFileItem item, string filePath) {
            if (string.Equals(item.FilePath, filePath, StringComparison.OrdinalIgnoreCase)) {
                return item;
            }

            foreach (var child in item.Children) {
                var found = FindItemRecursive(child, filePath);
                if (found != null) return found;
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

        // Lazy loading

        private WebFileItem CreateDirectoryItem(string folderPath, WebFileItem? parent) {
            var item = new WebFileItem(folderPath, WebFileItemType.Folder, parent);

            if (HasChildren(folderPath)) {
                var placeholder = new WebFileItem(
                    Path.Combine(folderPath, PlaceholderFileName),
                    WebFileItemType.File,
                    item) {
                    IsPlaceholder = true
                };
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
                var path = Path.Combine(
                    ProjectFolder,
                    manifestItem.Path.Replace('/', Path.DirectorySeparatorChar));

                if (manifestItem.Type == "folder") {
                    folder.Children.Add(CreateDirectoryItem(path, folder));
                }
                else {
                    folder.Children.Add(new WebFileItem(path, WebFileItemType.File, folder));
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
            var relativeFolder = Path.GetRelativePath(ProjectFolder, folderPath)
                .Replace(Path.DirectorySeparatorChar, '/');

            return relativeFolder == "." ? string.Empty : relativeFolder;
        }

        private void RebuildManifestChildrenIndex() {
            if (_manifestItems == null) {
                _manifestChildren = null;
                return;
            }

            _manifestChildren = new Dictionary<string, List<WebProjectManifestItem>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var item in _manifestItems) {
                var parentPath = Path.GetDirectoryName(item.Path)?
                    .Replace(Path.DirectorySeparatorChar, '/')
                    ?? string.Empty;

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

            if (typeOrder != otherTypeOrder) {
                return typeOrder - otherTypeOrder;
            }

            return string.Compare(
                Path.GetFileName(left.Path),
                Path.GetFileName(right.Path),
                StringComparison.OrdinalIgnoreCase);
        }

        // Tree modification

        private void AddItem(WebFileItem? parent, WebFileItem item) {
            var collection = parent?.Children ?? FileItems;

            if (collection.Contains(item)) return;

            var existing = collection.FirstOrDefault(
                x => string.Equals(x.FilePath, item.FilePath, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return;

            var index = 0;
            while (index < collection.Count && CompareFileItems(collection[index], item) <= 0) {
                index++;
            }

            collection.Insert(index, item);

            if (parent != null) {
                parent.IsExpanded = true;
            }
        }

        private void RemoveItem(WebFileItem item) {
            var collection = item.Parent?.Children ?? FileItems;
            collection.Remove(item);
        }

        private void SortItemInParent(WebFileItem item) {
            var parent = item.Parent;
            var collection = parent?.Children ?? FileItems;

            var oldIndex = collection.IndexOf(item);
            if (oldIndex < 0) return;

            collection.RemoveAt(oldIndex);

            var newIndex = 0;
            while (newIndex < collection.Count && CompareFileItems(collection[newIndex], item) <= 0) {
                newIndex++;
            }

            collection.Insert(newIndex, item);
        }

        private static int CompareFileItems(WebFileItem left, WebFileItem right) {
            if (left.Type != right.Type) {
                return left.Type == WebFileItemType.Folder ? -1 : 1;
            }

            return string.Compare(left.FileName, right.FileName, StringComparison.OrdinalIgnoreCase);
        }

        private static WebFileItem BuildItem(string path, WebFileItemType type, WebFileItem? parent) {
            return new WebFileItem(path, type, parent);
        }

        // File system events

        private void ApplyCreated(string path) {
            var parentPath = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(parentPath)) return;

            var parent = FindItem(parentPath);
            if (parent == null) return;

            EnsureChildrenLoaded(parent);

            if (Directory.Exists(path)) {
                AddItem(parent, CreateDirectoryItem(path, parent));
            }
            else if (File.Exists(path)) {
                AddItem(parent, new WebFileItem(path, WebFileItemType.File, parent));
            }
        }

        private void ApplyDeleted(string path) {
            var item = FindItem(path);
            if (item == null) return;

            RemoveItem(item);
        }

        private void ApplyRenamed(string oldPath, string newPath) {
            var item = FindItem(oldPath);
            if (item == null) return;

            RebindItemPath(item, newPath, item.Parent);
            SortItemInParent(item);
        }

        // Helpers

        private bool IsPathInManifest(string path) {
            if (_manifestItems == null) return false;

            var relativePath = Path.GetRelativePath(ProjectFolder, path)
                .Replace(Path.DirectorySeparatorChar, '/');

            return _manifestItems.Any(
                item => string.Equals(item.Path, relativePath, StringComparison.OrdinalIgnoreCase));
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

        // Fields

        private string _projectFolder = string.Empty;
        private string _filterText = string.Empty;
        private bool _isSearching;
        private bool _isCaseSensitive;
        private bool _isWholeWord;
        private const int MaxContentSearchMatches = 2000;
        private const long MaxContentSearchFileSize = 10 * 1024 * 1024;
        private const int MaxContentSearchPreviewLength = 240;
        private const int ContentSearchPreviewContextLength = 80;
        private const int SearchResultUiBatchSize = 25;
        private const string PlaceholderFileName = ".__lazy_placeholder__";
        private CancellationTokenSource? _filterDebounce;
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
