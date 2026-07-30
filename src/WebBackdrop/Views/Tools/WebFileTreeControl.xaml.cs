using System;
using System.Collections.Generic;
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
using VirtualPaper.UIComponent.Others;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.ViewModels;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.Models.SerializableData;
using Workloads.Creation.WebBackdrop.ViewModels;

/*
 * add 多项无法写入 vpw
 * 偶现  .lazy 文件
 * 
 */

namespace Workloads.Creation.WebBackdrop.Views.Tools {
    public sealed partial class WebFileTreeControl : UserControl {
        public event EventHandler<string>? FileOpenRequested;
        public event EventHandler<string>? FolderSelected;
        public event EventHandler<string>? NewFileRequested;

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
            var files = await WindowsStoragePickers.PickFilesAsync(hwnd, ["*"], multiSelect: true);
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

            foreach (var item in menuFlyout.Items) {
                switch (item) {
                    case MenuFlyoutItem { Name: "pasteMenuItem", Tag: WebFileItem pasteTarget } menuItem:
                        menuItem.IsEnabled = _viewModel.CanPasteTo(pasteTarget);
                        break;
                    case MenuFlyoutItem { Name: "folderDeleteMenuItem" }:
                        break; // Delete always enabled
                    case MenuFlyoutItem menuItem:
                        menuItem.IsEnabled = existsOnDisk;
                        break;
                    case MenuFlyoutSeparator sep when sep.Name is "folderEditSeparator1" or "folderEditSeparator2" or "folderExplorerSeparator":
                        sep.Visibility = existsOnDisk ? Visibility.Visible : Visibility.Collapsed;
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

            foreach (var item in menuFlyout.Items) {
                switch (item) {
                    case MenuFlyoutItem { Name: "fileDeleteMenuItem" }:
                        break; // Delete always enabled
                    case MenuFlyoutItem menuItem:
                        menuItem.IsEnabled = existsOnDisk;
                        break;
                    case MenuFlyoutSeparator sep when sep.Name is "fileEditSeparator" or "fileExplorerSeparator":
                        sep.Visibility = existsOnDisk ? Visibility.Visible : Visibility.Collapsed;
                        break;
                }
            }
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
