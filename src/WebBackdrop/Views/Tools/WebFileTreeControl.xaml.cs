using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.Models.SerializableData;
using Workloads.Creation.WebBackdrop.ViewModels;

/*
 * 修复 js/ html 误报、无法跳转文件的问题
 * 运行、调试
 * output 重定向
 * 导出为 zip、入库
 * 
 */

namespace Workloads.Creation.WebBackdrop.Views.Tools {
    public sealed partial class WebFileTreeControl : UserControl {
        public event EventHandler<string>? FileOpenRequested;
        public event EventHandler<string>? FolderSelected;
        public event EventHandler<string>? NewFileRequested;
        public event EventHandler<string>? ProjectFileRenamed;

        public WebFileItem? SelectedFileItem {
            get => (WebFileItem?)GetValue(SelectedFileItemProperty);
            set => SetValue(SelectedFileItemProperty, value);
        }
        public static readonly DependencyProperty SelectedFileItemProperty =
            DependencyProperty.Register(nameof(SelectedFileItem), typeof(WebFileItem), typeof(WebFileTreeControl), new PropertyMetadata(null));

        public WebFileTreeControl() {
            InitializeComponent();
            _viewModel = AppServiceLocator.Services.GetRequiredService<WebFileTreeViewModel>();
            _viewModel.ProjectFileRenamed += path => ProjectFileRenamed?.Invoke(this, path);
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
                FolderSelected?.Invoke(this, folder.FilePath);
                return;
            }

            if (args.InvokedItem is WebFileItem { Type: WebFileItemType.File } item) {
                if (!item.ExistsOnDisk) return;
                FileOpenRequested?.Invoke(this, item.FilePath);
            }
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
                await _viewModel.ImportExternalFileAsync(file.Path, target.FilePath);
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

        private async void RenameMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            await _viewModel.RenameAsync(item, GetRenamedPathAsync);
        }

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

            foreach (var item in menuFlyout.Items) {
                switch (item) {
                    case MenuFlyoutItem { Name: "fileCopyPathMenuItem" or "fileCopyRelativePathMenuItem" or "fileRevealMenuItem" } menuItem:
                        SetVisible(menuItem, isProjectFile || existsOnDisk);
                        break;
                    case MenuFlyoutItem { Name: "fileDeleteMenuItem" } menuItem:
                        SetVisible(menuItem, !isProjectFile);
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

        private async Task<string?> GetRenamedPathAsync(string path) {
            var oldName = Path.GetFileName(path);
            var viewModel = new RenameViewModel(oldName, 255, false);
            var dialogRes = await GlobalDialogUtils.ShowDialogAsync(
                new RenameView(viewModel),
                "Rename",
                "Confirm",
                "Cancel");

            if (dialogRes != DialogResult.Primary
                || !ComplianceUtil.IsValidPathSegmentName(viewModel.NewName)
                || string.Equals(oldName, viewModel.NewName, StringComparison.Ordinal)) {
                return null;
            }

            var newName = viewModel.NewName!;
            // 如果新名称没有后缀，则自动补上源文件的后缀
            if (!string.IsNullOrEmpty(Path.GetExtension(oldName))
                && string.IsNullOrEmpty(Path.GetExtension(newName))) {
                newName += Path.GetExtension(oldName);
            }

            return FileUtil.NextAvailablePath(Path.Combine(Path.GetDirectoryName(path)!, newName));
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
