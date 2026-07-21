using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.UIComponent.Others;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.ViewModels;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.ViewModels;

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

        public void Refresh(string projectFolder) {
            _viewModel.Refresh(projectFolder);
        }

        public void SelectFile(string filePath) {
            _viewModel.SelectFile(filePath);
        }

        private static void PreloadFolderOpenIcon() {
            _ = Application.Current.Resources.TryGetValue("WebBackdrop_FileTree_FolderOpen", out _);
        }

        private void FileTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args) {
            if (args.InvokedItem is WebFileItem { Type: WebFileItemType.Folder } folder) {
                folder.IsExpanded = !folder.IsExpanded;
                FolderSelected?.Invoke(this, folder.FilePath);
                return;
            }

            if (args.InvokedItem is WebFileItem { Type: WebFileItemType.File } item) {
                FileOpenRequested?.Invoke(this, item.FilePath);
            }
        }

        private async void NewFileMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            await _viewModel.CreateFileAsync(item.FilePath, GetRenamedPathAsync);
        }

        private async void NewFolderMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;

            await _viewModel.CreateFolderAsync(item.FilePath, GetRenamedPathAsync);
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

            foreach (var item in menuFlyout.Items) {
                if (item is MenuFlyoutItem { Name: "pasteMenuItem", Tag: WebFileItem target } menuItem) {
                    menuItem.IsEnabled = _viewModel.CanPasteTo(target);
                    return;
                }
            }
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
            if (!FileUtil.IsValidFileName(newName)) return null;
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
