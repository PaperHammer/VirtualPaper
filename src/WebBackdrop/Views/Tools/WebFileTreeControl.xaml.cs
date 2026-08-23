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
        // [已废弃，暂注释] 从未触发
        // public event EventHandler<string>? NewFileRequested;
        // public event EventHandler<string>? ProjectFileRenamed;

        public WebFileItem? SelectedFileItem {
            get => (WebFileItem?)GetValue(SelectedFileItemProperty);
            set => SetValue(SelectedFileItemProperty, value);
        }
        public static readonly DependencyProperty SelectedFileItemProperty =
            DependencyProperty.Register(nameof(SelectedFileItem), typeof(WebFileItem), typeof(WebFileTreeControl), new PropertyMetadata(null));

        public WebFileTreeControl() {
            InitializeComponent();
            _viewModel = AppServiceLocator.Services.GetRequiredService<WebFileTreeViewModel>();
            // [已废弃，暂注释] 上游事件从未触发
            // _viewModel.ProjectFileRenamed += path => ProjectFileRenamed?.Invoke(this, path);
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

        private async void FileTreeView_Drop(object sender, DragEventArgs e) {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

            // 命中目录则导入到该目录，否则导入到项目根目录
            var targetFolder = GetDropTargetFolder(e.GetPosition(fileTreeView))?.FilePath
                ?? _viewModel.ProjectFolder;

            var items = await e.DataView.GetStorageItemsAsync();
            foreach (var storageItem in items) {
                if (storageItem is StorageFile or StorageFolder) {
                    await _viewModel.ImportExternalAsync(storageItem.Path, targetFolder);
                }
            }
        }

        private void FileTreeView_DragItemsStarting(TreeView sender, TreeViewDragItemsStartingEventArgs args) {
            // 记录每个被拖条目的原始父级：原生拖拽会把条目从原集合移除并追加到落点集合，
            // 完成后需要凭原始父级判断“是否真的换了目录”并做回退。
            _dragOriginalParents.Clear();
            foreach (var dragged in args.Items.OfType<WebFileItem>()) {
                _dragOriginalParents[dragged] = dragged.Parent;
            }
        }

        private void FileTreeView_DragItemsCompleted(TreeView sender, TreeViewDragItemsCompletedEventArgs args) {
            // Esc/右键取消的拖拽没有落点，原生未做任何变更，无需处理
            if (args.DropResult == DataPackageOperation.None) {
                _dragOriginalParents.Clear();
                return;
            }

            foreach (var dragged in args.Items.OfType<WebFileItem>()) {
                _dragOriginalParents.TryGetValue(dragged, out var originalParent);
                _viewModel.MoveItemAsync(dragged, args.NewParentItem as WebFileItem, originalParent);
            }
            _dragOriginalParents.Clear();
        }

        /// <summary>
        /// 命中测试：找到拖放点下方的文件夹条目（命中文件时取其父目录）。
        /// </summary>
        private WebFileItem? GetDropTargetFolder(Windows.Foundation.Point position) {
            var elements = VisualTreeHelper.FindElementsInHostCoordinates(position, fileTreeView);
            foreach (var element in elements) {
                if (element is TreeViewItem { DataContext: WebFileItem item }) {
                    return item.Type == WebFileItemType.Folder ? item : item.Parent;
                }
            }
            return null;
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
        // 拖拽开始时的条目原始父级（条目 → 父级），完成后凭此判断是否需要移动/回退
        private readonly Dictionary<WebFileItem, WebFileItem?> _dragOriginalParents = [];
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
