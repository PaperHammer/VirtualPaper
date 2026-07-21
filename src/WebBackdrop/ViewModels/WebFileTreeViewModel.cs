using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Models.Mvvm;
using Workloads.Creation.WebBackdrop.Models;

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

        public void Refresh(string projectFolder) {
            if (!Directory.Exists(projectFolder)) return;

            ProjectFolder = projectFolder;
            var root = BuildDirectoryItem(projectFolder);
            root.IsExpanded = true;
            FileItems.Clear();
            FileItems.Add(root);
        }

        public void SelectFile(string filePath) {
            var item = FindItem(filePath);
            if (item == null) return;

            ExpandParents(item);
        }

        public async Task CreateFileAsync(string folderPath, Func<string, Task<string?>> getRenamedPathAsync) {
            var path = await getRenamedPathAsync(Path.Combine(folderPath, "New File.txt"));
            if (path == null) return;

            File.WriteAllText(path, string.Empty);
            var parent = FindItem(folderPath);
            AddItem(parent, new WebFileItem(path, WebFileItemType.File, parent));
        }

        public async Task CreateFolderAsync(string folderPath, Func<string, Task<string?>> getRenamedPathAsync) {
            var path = await getRenamedPathAsync(Path.Combine(folderPath, "New Folder"));
            if (path == null) return;

            Directory.CreateDirectory(path);
            var parent = FindItem(folderPath);
            AddItem(parent, new WebFileItem(path, WebFileItemType.Folder, parent));
        }

        public async Task RenameAsync(WebFileItem item, Func<string, Task<string?>> getRenamedPathAsync) {
            var path = await getRenamedPathAsync(item.FilePath);
            if (path == null) return;

            if (item.Type == WebFileItemType.File) {
                File.Move(item.FilePath, path);
            }
            else {
                Directory.Move(item.FilePath, path);
            }

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

        public void Delete(WebFileItem item) {
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
                ? BuildDirectoryItem(path, parent)
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
        private WebFileItem? _clipboardItem;
        private WebFileTreeClipboardOperation _clipboardOperation;
    }

    public enum WebFileTreeClipboardOperation {
        None,
        Cut,
        Copy,
    }
}
