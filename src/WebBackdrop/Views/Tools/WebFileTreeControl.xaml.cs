using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.ProjectSystem.Events;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Others;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.Extensions;
using VirtualPaper.UIComponent.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.Models.SerializableData;
using Workloads.Creation.WebBackdrop.ViewModels;

/*
 * TODO:
 * 回到上一个光标位置
 * output 重定向
 * web 项目的 basicinfo 的显示
 */

namespace Workloads.Creation.WebBackdrop.Views.Tools {
    public sealed partial class WebFileTreeControl : UserControl {
        public event EventHandler<string>? FileOpenRequested;
        public event EventHandler<string>? FileSaveRequested;
        public event EventHandler<string>? FileSaveAsRequested;
        public event EventHandler<string>? FolderSelected;

        public WebFileItem? SelectedFileItem {
            get => (WebFileItem?)GetValue(SelectedFileItemProperty);
            set => SetValue(SelectedFileItemProperty, value);
        }
        public static readonly DependencyProperty SelectedFileItemProperty =
            DependencyProperty.Register(nameof(SelectedFileItem), typeof(WebFileItem), typeof(WebFileTreeControl), new PropertyMetadata(null));

        public WebFileTreeControl() {
            InitializeComponent();
            _viewModel = AppServiceLocator.Services.GetRequiredService<WebFileTreeViewModel>();
            DataContext = _viewModel;
            PreloadFolderOpenIcon();
        }

        public void Refresh(WebDesignFileUtil designFileUtil) {
            _viewModel.Refresh(designFileUtil);
        }

        public void Refresh(string projectFolder) {
            _viewModel.Refresh(projectFolder);
        }

        public void SyncManifest(WebDesignFileUtil designFileUtil) {
            _viewModel.SyncManifest(designFileUtil);
        }

        public void SelectFile(string filePath) {
            _viewModel.SelectFile(filePath);
            var item = _viewModel.FindItem(filePath.Replace('/', Path.DirectorySeparatorChar));
            if (item != null) {
                SelectedFileItem = item;
                fileTreeView.SelectedItem = item;
            }
        }

        public void SetFileSaved(string filePath, bool isSaved) {
            _viewModel.SetFileSaved(filePath, isSaved);
        }

        public void ApplyChange(ProjectChangedEvent e) {
            _viewModel.ApplyChange(e);
        }

        private static void PreloadFolderOpenIcon() {
            _ = Application.Current.Resources.TryGetValue("WebBackdrop_FileTree_FolderOpen", out _);
        }

        private void FileTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args) {
            if (args.InvokedItem is WebFileItem { Type: WebFileItemType.Folder } folder) {
                _viewModel.ToggleFolder(folder);
                folder.IsExpanded = !folder.IsExpanded;
                FolderSelected?.Invoke(this, folder.FilePath);
                return;
            }

            if (args.InvokedItem is WebFileItem { Type: WebFileItemType.File } item) {
                if (!item.ExistsOnDisk) return;
                FileOpenRequested?.Invoke(this, item.FilePath);
            }
        }

        private void FileTreeView_DragOver(object sender, DragEventArgs e) {
            // 外部文件/文件夹：复制导入项目；内部条目拖动由 TreeView 原生处理，
            // 这里不改 AcceptedOperation，避免覆盖原生 Move 判定。
            if (e.DataView.Contains(StandardDataFormats.StorageItems)) {
                e.AcceptedOperation = DataPackageOperation.Copy;
            }
        }

        /// <summary>
        /// 处理外部文件/文件夹拖拽到 TreeView 的导入逻辑
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void FileTreeView_Drop(object sender, DragEventArgs e) {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

            // 命中目录则导入到该目录，否则导入到项目根目录
            var targetFolder = GetDropTargetFolder(e.GetPosition(fileTreeView))?.FilePath ?? _viewModel.ProjectFolder;
            var items = await e.DataView.GetStorageItemsAsync();
            foreach (var storageItem in items) {
                if (storageItem is StorageFile or StorageFolder) {
                    await _viewModel.ImportExternalAsync(storageItem.Path, targetFolder);
                }
            }
        }

        /// <summary>
        /// 处理 TreeView 内部条目拖拽的移动逻辑
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void FileTreeView_DragItemsCompleted(TreeView sender, TreeViewDragItemsCompletedEventArgs args) {
            if (args.DropResult == DataPackageOperation.None) return;
            if (args.NewParentItem is not WebFileItem targetItem) return;

            var draggedItems = args.Items.OfType<WebFileItem>().Distinct().ToArray();
            if (draggedItems.Length == 0) return;

            // 原生拖拽只改视觉节点、不动数据源：摘掉旧节点 → VM 真实移动 → 按数据顺序回插节点
            var originalParents = draggedItems.ToDictionary(item => item, item => item.Parent);
            var nodeMap = BuildNodeMap(sender.RootNodes);

            var draggedNodes = draggedItems
                .Where(nodeMap.ContainsKey)
                .Select(item => (Item: item, Node: nodeMap[item]))
                .ToList();

            foreach (var (_, node) in draggedNodes) {
                node.Parent?.Children.Remove(node);
            }

            _viewModel.MoveItems(draggedItems, targetItem, originalParents);

            foreach (var (item, node) in draggedNodes) {
                var parentItem = item.Parent;
                var parentNode = parentItem != null
                    ? (nodeMap.TryGetValue(parentItem, out var parentNodeOfItem) ? parentNodeOfItem : null)
                    : sender.RootNodes.FirstOrDefault();
                if (parentNode == null) continue;

                // VM 修改数据集合后，TreeView 会按“数量差”自动为新条目创建节点
                //（我们已把旧节点摘除，数据侧 Add 时节点数少 1 → 自动补建）。
                // 此时无需再插回旧节点，否则同一文件会出现两行。
                var alreadyInserted = false;
                foreach (var child in parentNode.Children) {
                    if (child.Content is WebFileItem childItem && childItem.Equals(item)) {
                        alreadyInserted = true;
                        break;
                    }
                }
                if (alreadyInserted) continue;

                var collection = parentItem?.Children ?? _viewModel.FileItems;
                var index = collection.IndexOf(item);
                if (index < 0) index = collection.Count;

                parentNode.Children.Insert(Math.Min(index, parentNode.Children.Count), node);
            }
        }

        private WebFileItem? GetDropTargetFolder(Point position) {
            var elements = VisualTreeHelper.FindElementsInHostCoordinates(position, fileTreeView);
            foreach (var element in elements) {
                if (element is TreeViewItem treeViewItem && treeViewItem.DataContext is WebFileItem item) {
                    return item.Type == WebFileItemType.Folder ? item : item.Parent;
                }
            }

            return null;
        }

        /// <summary>
        /// 一次性构建 数据条目 → TreeViewNode 映射，避免每次查找都递归遍历整棵树。
        /// </summary>
        private static Dictionary<WebFileItem, TreeViewNode> BuildNodeMap(IList<TreeViewNode> nodes) {
            var map = new Dictionary<WebFileItem, TreeViewNode>(ReferenceEqualityComparer.Instance);

            void Visit(TreeViewNode node) {
                if (node.Content is WebFileItem item && !map.ContainsKey(item)) {
                    map[item] = node;
                }

                foreach (var child in node.Children) {
                    Visit(child);
                }
            }

            foreach (var root in nodes) {
                Visit(root);
            }

            return map;
        }

        private async void NewFileMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            await _viewModel.CreateFileAsync(item.FilePath, GetAddFileOrFolderItemPathAsync);
        }

        private async void NewFolderMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            await _viewModel.CreateFolderAsync(item.FilePath, GetAddFileOrFolderItemPathAsync);
        }

        private async void AddItemsMenuItem_Click(object sender, RoutedEventArgs e) {
            var target = GetMenuItemTarget(sender);
            if (target == null) return;

            var hwnd = VirtualPaper.Common.Constants.Runtime.MainWindowHwnd;
            if (hwnd == IntPtr.Zero) {
                hwnd = AppServiceLocator.Services.GetRequiredService<Microsoft.UI.Xaml.Window>().GetWindowHandleEx();
            }
            var files = await WindowsStoragePickers.PickFilesAsync(WindowConsts.WindowHandle, ["*"], multiSelect: true);
            if (files.Length == 0) return;

            foreach (var file in files) {
                await _viewModel.ImportExternalAsync(file.Path, target.FilePath);
            }
        }

        private void CutMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            _viewModel.Cut(item);
        }

        private void CopyMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            _viewModel.Copy(item);
        }

        private void PasteMenuItem_Click(object sender, RoutedEventArgs e) {
            var target = GetMenuItemTarget(sender);
            if (target == null) return;

            _viewModel.PasteTo(target);
        }

        private void CopyPathMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            ClipboardUtil.Copy(item.FilePath);
        }

        private void CopyRelativePathMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            ClipboardUtil.Copy(FileUtil.GetRelativePath(_viewModel.ProjectFolder, item.FilePath));
        }

        private void SaveMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            FileSaveRequested?.Invoke(this, item.FilePath);
        }

        private void SaveAsMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            FileSaveAsRequested?.Invoke(this, item.FilePath);
        }

        private void RenameMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            // VS Code 风格：不弹窗，直接在名称区进入行内重命名
            _viewModel.BeginRename(item);

            // 模板内的 TextBox 只在首次实例化时触发 Loaded，
            // 进入重命名模式后手动聚焦并选中后缀名之前的部分
            DispatcherQueue.TryEnqueue(() => FocusRenameTextBox(item));
        }

        private void FocusRenameTextBox(WebFileItem item) {
            var textBox = FindRenameTextBox(fileTreeView.ContainerFromItem(item), item);
            if (textBox == null) return;

            textBox.Focus(FocusState.Programmatic);
            var extensionLength = item.Type == WebFileItemType.File
                ? Path.GetExtension(item.RenameText).Length
                : 0;
            textBox.Select(0, Math.Max(0, textBox.Text.Length - extensionLength));
        }

        private static TextBox? FindRenameTextBox(DependencyObject? root, WebFileItem item) {
            if (root == null) return null;

            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++) {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is TextBox textBox && ReferenceEquals(textBox.Tag, item)) return textBox;

                var found = FindRenameTextBox(child, item);
                if (found != null) return found;
            }
            return null;
        }

        private void RenameTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (sender is not TextBox textBox || textBox.Tag is not WebFileItem item) return;

            // 输入过程中实时校验：与提交时完全相同的判定逻辑
            item.IsRenameInvalid = IsInvalidRenameName(textBox.Text.Trim());
        }

        private async void RenameTextBox_KeyDown(object sender, KeyRoutedEventArgs e) {
            if (sender is not TextBox textBox || textBox.Tag is not WebFileItem item) return;

            if (e.Key == Windows.System.VirtualKey.Enter) {
                e.Handled = true;
                await TryCommitRenameAsync(textBox, item);
            }
            else if (e.Key == Windows.System.VirtualKey.Escape) {
                e.Handled = true;
                _viewModel.CancelRename();
            }
        }

        private async void RenameTextBox_LostFocus(object sender, RoutedEventArgs e) {
            if (sender is not TextBox textBox || textBox.Tag is not WebFileItem item) return;
            if (!item.IsRenaming) return; // 已提交或已取消

            // 失焦即提交；名称非法时保持重命名模式并重新聚焦
            await TryCommitRenameAsync(textBox, item);
            if (item.IsRenaming && item.IsRenameInvalid) {
                textBox.Focus(FocusState.Programmatic);
            }
        }

        private async Task TryCommitRenameAsync(TextBox textBox, WebFileItem item) {
            var name = textBox.Text.Trim();
            if (IsInvalidRenameName(name)) {
                item.IsRenameInvalid = true;
                return;
            }

            await _viewModel.RenameToAsync(item, name);
        }

        private static bool IsInvalidRenameName(string name)
            => string.IsNullOrWhiteSpace(name) || !ComplianceUtil.IsValidPathSegmentName(name);

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            _viewModel.Delete(item);
        }

        private void RevealInExplorerMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            FileUtil.OpenFolderByExplorer(item.FilePath);
        }

        private void FolderMenuFlyout_Opening(object sender, object e) {
            if (sender is not MenuFlyout menuFlyout) return;

            var folderItem = menuFlyout.Items.OfType<MenuFlyoutItem>()
                .FirstOrDefault()?.Tag as WebFileItem;
            if (folderItem == null) return;

            var existsOnDisk = folderItem.ExistsOnDisk;
            var isProjectFile = _viewModel.IsProjectFileItem(folderItem);

            foreach (var item in menuFlyout.Items) {
                switch (item) {
                    case MenuFlyoutItem { Name: "folderCopyPathMenuItem" or "folderCopyRelativePathMenuItem" or "folderRevealMenuItem" } menuItem:
                        SetVisible(menuItem, isProjectFile || existsOnDisk);
                        break;
                    case MenuFlyoutItem { Name: "pasteMenuItem", Tag: WebFileItem pasteTarget } menuItem:
                        SetVisible(menuItem, !isProjectFile && existsOnDisk && _viewModel.CanPasteTo(pasteTarget));
                        break;
                    case MenuFlyoutItem menuItem:
                        SetVisible(menuItem, !isProjectFile && existsOnDisk);
                        break;
                    case MenuFlyoutSeparator sep:
                        sep.Visibility = !isProjectFile && existsOnDisk ? Visibility.Visible : Visibility.Collapsed;
                        break;
                }
            }
        }

        private void FileMenuFlyout_Opening(object sender, object e) {
            if (sender is not MenuFlyout menuFlyout) return;

            var fileItem = menuFlyout.Items.OfType<MenuFlyoutItem>()
                .FirstOrDefault()?.Tag as WebFileItem;
            if (fileItem == null) return;

            var existsOnDisk = fileItem.ExistsOnDisk;
            var isProjectFile = _viewModel.IsProjectFileItem(fileItem);
            var isEditableFile = WebEditorFileUtil.IsTextExtension(Path.GetExtension(fileItem.FilePath));

            foreach (var item in menuFlyout.Items) {
                switch (item) {
                    case MenuFlyoutItem { Name: "fileCopyPathMenuItem" or "fileCopyRelativePathMenuItem" or "fileRevealMenuItem" } menuItem:
                        SetVisible(menuItem, isProjectFile || existsOnDisk);
                        break;
                    case MenuFlyoutItem { Name: "fileDeleteMenuItem" } menuItem:
                        SetVisible(menuItem, !isProjectFile);
                        break;
                    case MenuFlyoutItem { Name: "fileSaveMenuItem" or "fileSaveAsMenuItem" } menuItem:
                        SetVisible(menuItem, !isProjectFile && existsOnDisk && isEditableFile);
                        break;
                    case MenuFlyoutSeparator { Name: "fileSaveSeparator" } sep:
                        sep.Visibility = !isProjectFile && existsOnDisk && isEditableFile ? Visibility.Visible : Visibility.Collapsed;
                        break;
                    case MenuFlyoutItem menuItem:
                        SetVisible(menuItem, !isProjectFile && existsOnDisk);
                        break;
                    case MenuFlyoutSeparator sep:
                        sep.Visibility = !isProjectFile && existsOnDisk ? Visibility.Visible : Visibility.Collapsed;
                        break;
                }
            }
        }

        private static void SetVisible(MenuFlyoutItem item, bool visible) {
            item.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task<string?> GetAddFileOrFolderItemPathAsync(string path) {
            var defaultPath = FileUtil.NextAvailablePath(path);
            var defaultName = Path.GetFileName(defaultPath);
            var viewModel = new AddFileItemViewModel(defaultName, 255, false);
            var dialogRes = await GlobalDialogUtils.ShowDialogAsync(
                new AddFileItemView(viewModel),
                "Add",
                "Confirm",
                "Cancel");

            if (dialogRes != DialogResult.Primary
                || !ComplianceUtil.IsValidPathSegmentName(viewModel.NewName)) {
                return null;
            }

            var newName = viewModel.NewName!;
            return FileUtil.NextAvailablePath(Path.Combine(Path.GetDirectoryName(path)!, newName));
        }

        private static WebFileItem? GetMenuItemTarget(object sender) {
            return sender is FrameworkElement { Tag: WebFileItem item } ? item : null;
        }

        private readonly WebFileTreeViewModel _viewModel;
    }

    partial class WebFileItemTemplateSelector : DataTemplateSelector {
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
