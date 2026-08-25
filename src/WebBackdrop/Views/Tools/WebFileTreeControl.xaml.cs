using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.ProjectSystem.Events;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Collection;
using VirtualPaper.UIComponent.Others;
using VirtualPaper.UIComponent.Templates;
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
using Workloads.Utils.DraftUtils.Interfaces;

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
        public event EventHandler<WebContentSearchMatch>? ContentMatchOpenRequested;
        public event EventHandler<WebFileMovedEventArgs>? ActiveFileMoved;
        public event EventHandler? CommandStateChanged;

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
            Loaded += WebFileTreeControl_Loaded;
            Unloaded += WebFileTreeControl_Unloaded;

            // TreeView 内部（TreeViewList / TreeViewItem）会先处理并标记 Handled 拖拽事件，
            // XAML 属性绑定（handledEventsToo=false）的外部 Drop 因此不触发；
            // 这里用 handledEventsToo=true 注册，确保外部文件拖入的 DragOver / Drop 一定能收到。
            fileTreeView.AddHandler(UIElement.DragOverEvent, new DragEventHandler(FileTreeView_DragOver), true);
            fileTreeView.AddHandler(UIElement.DropEvent, new DragEventHandler(FileTreeView_Drop), true);
            AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(FileTree_PreviewKeyDown), true);
        }

        private void FileTree_PreviewKeyDown(object sender, KeyRoutedEventArgs e) {
            if (e.Key != Windows.System.VirtualKey.Escape) return;

            var clearSystemClipboard = IsOwnedSystemClipboard();
            if (!_viewModel.CancelCut()) return;

            e.Handled = true;
            if (clearSystemClipboard) {
                Clipboard.Clear();
            }
        }

        public void Refresh(WebDesignFileUtil designFileUtil) {
            _viewModel.Refresh(designFileUtil);
            RestoreActiveFileSelection();
        }

        public void Refresh(string projectFolder) {
            _viewModel.Refresh(projectFolder);
            RestoreActiveFileSelection();
        }

        public void SyncManifest(WebDesignFileUtil designFileUtil) {
            _viewModel.SyncManifest(designFileUtil);
            RestoreActiveFileSelection();
        }

        public void SelectFile(string filePath) {
            _activeFilePath = Path.GetFullPath(filePath.Replace('/', Path.DirectorySeparatorChar));
            RestoreActiveFileSelection();
        }

        public void ClearActiveFileSelection() {
            _activeFilePath = null;
            if (_activeFileItem != null) {
                _activeFileItem.IsActiveFile = false;
                _activeFileItem = null;
            }
            SelectedFileItem = null;
            fileTreeView.SelectedItem = null;
            CommandStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void FocusContentSearch() {
            contentSearchBox.Focus(FocusState.Programmatic);
            contentSearchBox.SelectAll();
        }

        public async Task CutCommandAsync() {
            var item = GetCommandTarget();
            if (!CanModify(item)) return;

            _viewModel.Cut(item!);
            await PublishStorageItemToClipboardAsync(item!, DataPackageOperation.Move);
        }

        public async Task CopyCommandAsync() {
            var item = GetCommandTarget();
            if (!CanModify(item)) return;

            _viewModel.Copy(item!);
            await PublishStorageItemToClipboardAsync(item!, DataPackageOperation.Copy);
        }

        public Task PasteCommandAsync() {
            var target = ResolvePasteTarget(GetCommandTarget());
            return target == null ? Task.CompletedTask : PasteToAsync(target);
        }

        public bool CanExecuteCommand(RuntimeEditCommand command) {
            var item = GetCommandTarget();
            return command switch {
                RuntimeEditCommand.Cut or
                RuntimeEditCommand.Copy or
                RuntimeEditCommand.Rename or
                RuntimeEditCommand.Delete => CanModify(item),
                RuntimeEditCommand.CopyPath or
                RuntimeEditCommand.CopyRelativePath => item != null
                    && (item.ExistsOnDisk || _viewModel.IsProjectFileItem(item)),
                RuntimeEditCommand.Paste => CanPasteTo(ResolvePasteTarget(item)),
                _ => false,
            };
        }

        public void CopyPathCommand() {
            var item = GetCommandTarget();
            if (item == null) return;
            ClipboardUtil.Copy(item.FilePath);
        }

        public void CopyRelativePathCommand() {
            var item = GetCommandTarget();
            if (item == null) return;
            ClipboardUtil.Copy(FileUtil.GetRelativePath(_viewModel.ProjectFolder, item.FilePath));
        }

        public void RenameCommand() {
            var item = GetCommandTarget();
            if (!CanModify(item)) return;

            BeginRename(item!);
        }

        public void DeleteCommand() {
            var item = GetCommandTarget();
            if (!CanModify(item)) return;
            _viewModel.Delete(item!);
        }

        private void ContentSearchBox_KeyDown(object sender, KeyRoutedEventArgs e) {
            if (e.Key != Windows.System.VirtualKey.Escape) return;

            e.Handled = true;
            _viewModel.FilterText = string.Empty;
        }

        private void RestoreActiveFileSelection() {
            if (string.IsNullOrEmpty(_activeFilePath) || _isRestoringSelection) return;

            _viewModel.SelectFile(_activeFilePath);
            var item = _viewModel.FindItem(_activeFilePath);
            if (item == null) return;

            SetActiveFileSelection(item);

            // 活动样式由 IsActiveFile 直接驱动；这里只等待深层容器生成并滚动到可视区域。
            QueueActiveFileBringIntoView(_activeFilePath, 0);
        }

        private void QueueActiveFileBringIntoView(string expectedPath, int attempt) {
            DispatcherQueue.TryEnqueue(
                Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                () => {
                    if (!string.Equals(expectedPath, _activeFilePath, StringComparison.OrdinalIgnoreCase)) return;

                    var realizedItem = _viewModel.FindItem(expectedPath);
                    if (realizedItem == null) return;

                    fileTreeView.UpdateLayout();
                    if (fileTreeView.ContainerFromItem(realizedItem) is UIElement container) {
                        container.StartBringIntoView();
                        return;
                    }

                    // 每一轮最多实现下一层展开容器；深目录有限次跨帧重试，不阻塞 UI。
                    if (attempt + 1 < MaxSelectionRealizationAttempts) {
                        QueueActiveFileBringIntoView(expectedPath, attempt + 1);
                    }
                });
        }

        private void SetActiveFileSelection(WebFileItem item) {
            if (!ReferenceEquals(_activeFileItem, item)) {
                if (_activeFileItem != null) {
                    _activeFileItem.IsActiveFile = false;
                }
                _activeFileItem = item;
            }
            item.IsActiveFile = true;

            _isRestoringSelection = true;
            try {
                SelectedFileItem = item;
                fileTreeView.SelectedItem = item;
            }
            finally {
                _isRestoringSelection = false;
            }
        }

        private void WebFileTreeControl_Loaded(object sender, RoutedEventArgs e) {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            Clipboard.ContentChanged -= Clipboard_ContentChanged;
            Clipboard.ContentChanged += Clipboard_ContentChanged;
            RestoreActiveFileSelection();
            DispatcherQueue.TryEnqueue(AttachLeftPanelScrollBars);
        }

        private void WebFileTreeControl_Unloaded(object sender, RoutedEventArgs e) {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel.CancelPendingSearch();
            Clipboard.ContentChanged -= Clipboard_ContentChanged;
            DetachFileTreeScrollViewer();
            UnregisterScrollBarIndicatorCallbacks();
            SetContextMenuTarget(null);
        }

        private void Clipboard_ContentChanged(object? sender, object e) {
            if (_viewModel.HasCutItem && !IsOwnedSystemClipboard()) {
                _viewModel.CancelCut();
            }
            CommandStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(WebFileTreeViewModel.IsSearchMode)) {
                DispatcherQueue.TryEnqueue(RestoreActiveFileSelection);
                DispatcherQueue.TryEnqueue(AttachLeftPanelScrollBars);
                QueueStickyFoldersUpdate();
            }
        }

        private void AttachLeftPanelScrollBars() {
            AttachFileTreeScrollViewer();

            var fileTreeScrollBar = FindVerticalScrollBar(_fileTreeScrollViewer);
            contentSearchResultsList.UpdateLayout();
            var contentSearchScrollBar = FindVerticalScrollBar(
                FindDescendant<ScrollViewer>(contentSearchResultsList));

            ReplaceTrackedScrollBar(
                ref _fileTreeVerticalScrollBar,
                ref _fileTreeScrollBarIndicatorToken,
                fileTreeScrollBar);
            ReplaceTrackedScrollBar(
                ref _contentSearchVerticalScrollBar,
                ref _contentSearchScrollBarIndicatorToken,
                contentSearchScrollBar);
        }

        private void LeftPanel_PointerEntered(object sender, PointerRoutedEventArgs e) {
            _isPointerOverLeftPanel = true;
            AttachLeftPanelScrollBars();
            SetLeftPanelScrollBarsVisible(true);
        }

        private void LeftPanel_PointerExited(object sender, PointerRoutedEventArgs e) {
            _isPointerOverLeftPanel = false;
            SetLeftPanelScrollBarsVisible(false);
        }

        private void ReplaceTrackedScrollBar(
            ref ScrollBar? current,
            ref long? callbackToken,
            ScrollBar? replacement) {
            if (ReferenceEquals(current, replacement)) {
                SetLeftPanelScrollBarVisible(current, _isPointerOverLeftPanel);
                return;
            }

            UnregisterScrollBarIndicatorCallback(ref current, ref callbackToken);
            current = replacement;
            if (current == null) return;

            current.OpacityTransition ??= new ScalarTransition {
                Duration = TimeSpan.FromMilliseconds(160),
            };
            callbackToken = current.RegisterPropertyChangedCallback(
                ScrollBar.IndicatorModeProperty,
                ScrollBar_IndicatorModeChanged);
            SetLeftPanelScrollBarVisible(current, _isPointerOverLeftPanel);
        }

        private void ScrollBar_IndicatorModeChanged(DependencyObject sender, DependencyProperty dp) {
            if (!_isPointerOverLeftPanel
                || sender is not ScrollBar scrollBar
                || scrollBar.IndicatorMode == ScrollingIndicatorMode.MouseIndicator) {
                return;
            }

            DispatcherQueue.TryEnqueue(() => {
                if (_isPointerOverLeftPanel) {
                    scrollBar.IndicatorMode = ScrollingIndicatorMode.MouseIndicator;
                }
            });
        }

        private void UnregisterScrollBarIndicatorCallbacks() {
            UnregisterScrollBarIndicatorCallback(
                ref _fileTreeVerticalScrollBar,
                ref _fileTreeScrollBarIndicatorToken);
            UnregisterScrollBarIndicatorCallback(
                ref _contentSearchVerticalScrollBar,
                ref _contentSearchScrollBarIndicatorToken);
        }

        private static void UnregisterScrollBarIndicatorCallback(
            ref ScrollBar? scrollBar,
            ref long? callbackToken) {
            if (scrollBar != null && callbackToken.HasValue) {
                scrollBar.UnregisterPropertyChangedCallback(
                    ScrollBar.IndicatorModeProperty,
                    callbackToken.Value);
            }
            scrollBar = null;
            callbackToken = null;
        }

        private void SetLeftPanelScrollBarsVisible(bool visible) {
            SetLeftPanelScrollBarVisible(_fileTreeVerticalScrollBar, visible);
            SetLeftPanelScrollBarVisible(_contentSearchVerticalScrollBar, visible);
        }

        private static void SetLeftPanelScrollBarVisible(ScrollBar? scrollBar, bool visible) {
            if (scrollBar == null) return;

            scrollBar.IndicatorMode = visible
                ? ScrollingIndicatorMode.MouseIndicator
                : ScrollingIndicatorMode.None;
            scrollBar.Opacity = visible ? 1 : 0;
        }

        private static ScrollBar? FindVerticalScrollBar(DependencyObject? root) {
            if (root == null) return null;

            var scrollBars = new List<ScrollBar>();
            CollectDescendants(root, scrollBars);
            return scrollBars.FirstOrDefault(scrollBar => scrollBar.Orientation == Orientation.Vertical);
        }

        private void AttachFileTreeScrollViewer() {
            var scrollViewer = FindDescendant<ScrollViewer>(fileTreeView);
            if (ReferenceEquals(_fileTreeScrollViewer, scrollViewer)) return;

            DetachFileTreeScrollViewer();
            _fileTreeScrollViewer = scrollViewer;
            if (_fileTreeScrollViewer != null) {
                _fileTreeScrollViewer.ViewChanged += FileTreeScrollViewer_ViewChanged;
            }
            QueueStickyFoldersUpdate();
        }

        private void DetachFileTreeScrollViewer() {
            if (_fileTreeScrollViewer != null) {
                _fileTreeScrollViewer.ViewChanged -= FileTreeScrollViewer_ViewChanged;
                _fileTreeScrollViewer = null;
            }
            _stickyFolders.Clear();
            stickyFoldersPanel.Visibility = Visibility.Collapsed;
        }

        private void FileTreeScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e) {
            QueueStickyFoldersUpdate();
        }

        private void QueueStickyFoldersUpdate() {
            if (_stickyFoldersUpdateQueued) return;
            _stickyFoldersUpdateQueued = true;
            DispatcherQueue.TryEnqueue(
                Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                () => {
                    _stickyFoldersUpdateQueued = false;
                    UpdateStickyFolders();
                });
        }

        private void UpdateStickyFolders() {
            if (_viewModel.IsSearchMode
                || _fileTreeScrollViewer == null
                || _fileTreeScrollViewer.VerticalOffset <= 0.5) {
                SetStickyFolders([]);
                return;
            }

            var realizedItems = new List<ArcTreeViewItem>();
            CollectDescendants(fileTreeView, realizedItems);

            WebFileItem? firstVisibleItem = null;
            var firstVisibleTop = double.MaxValue;
            foreach (var container in realizedItems) {
                if (container.DataContext is not WebFileItem item || container.ActualHeight <= 0) continue;

                try {
                    var top = container.TransformToVisual(fileTreeView).TransformPoint(new Point()).Y;
                    if (top + container.ActualHeight <= 0 || top >= firstVisibleTop) continue;
                    firstVisibleTop = top;
                    firstVisibleItem = item;
                }
                catch (ArgumentException) {
                    // 容器可能恰好在滚动过程中被虚拟化；下一帧会重新计算。
                }
            }

            var folder = firstVisibleItem?.Type == WebFileItemType.Folder && firstVisibleTop < 0
                ? firstVisibleItem
                : firstVisibleItem?.Parent;
            var chain = new List<WebFileItem>();
            while (folder != null) {
                chain.Add(folder);
                folder = folder.Parent;
            }
            chain.Reverse();
            SetStickyFolders(chain);
        }

        private void SetStickyFolders(IReadOnlyList<WebFileItem> folders) {
            if (_stickyFolders.Count == folders.Count
                && !_stickyFolders.Where((item, index) => !ReferenceEquals(item, folders[index])).Any()) {
                return;
            }

            _stickyFolders.Clear();
            foreach (var folder in folders) {
                _stickyFolders.Add(folder);
            }
            stickyFoldersPanel.Visibility = _stickyFolders.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void StickyFolder_Click(object sender, RoutedEventArgs e) {
            if (sender is not FrameworkElement { Tag: WebFileItem folder }) return;

            folder.IsExpanded = true;
            FolderSelected?.Invoke(this, folder.FilePath);
            if (fileTreeView.ContainerFromItem(folder) is UIElement container) {
                container.StartBringIntoView();
            }
            QueueStickyFoldersUpdate();
        }

        private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++) {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T result) return result;

                var descendant = FindDescendant<T>(child);
                if (descendant != null) return descendant;
            }
            return null;
        }

        private static void CollectDescendants<T>(DependencyObject root, ICollection<T> results)
            where T : DependencyObject {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++) {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T result) results.Add(result);
                CollectDescendants(child, results);
            }
        }

        public void SetFileSaved(string filePath, bool isSaved) {
            _viewModel.SetFileSaved(filePath, isSaved);
        }

        public void ApplyChange(ProjectChangedEvent e) {
            if (e.Type == ProjectChangeType.Renamed
                && !string.IsNullOrEmpty(_activeFilePath)
                && string.Equals(e.OldPath, _activeFilePath, StringComparison.OrdinalIgnoreCase)) {
                _activeFilePath = Path.GetFullPath(e.Path);
            }

            _viewModel.ApplyChange(e);
            RestoreActiveFileSelection();
        }

        private void FileTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args) {
            if (args.InvokedItem is WebFileItem { Type: WebFileItemType.Folder } folder) {
                _viewModel.ToggleFolder(folder);
                folder.IsExpanded = !folder.IsExpanded;
                FolderSelected?.Invoke(this, folder.FilePath);
                DispatcherQueue.TryEnqueue(RestoreActiveFileSelection);
                QueueStickyFoldersUpdate();
                return;
            }

            if (args.InvokedItem is WebFileItem { Type: WebFileItemType.File } item) {
                if (!item.ExistsOnDisk) {
                    DispatcherQueue.TryEnqueue(RestoreActiveFileSelection);
                    return;
                }
                FileOpenRequested?.Invoke(this, item.FilePath);
            }
        }

        private void FileTreeView_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args) {
            CommandStateChanged?.Invoke(this, EventArgs.Empty);
            if (_isRestoringSelection || string.IsNullOrEmpty(_activeFilePath)) return;

            // 文件点击将驱动编辑器切换，允许它暂时成为选择；目录或空选择不能覆盖活动文件。
            if (fileTreeView.SelectedItem is WebFileItem { Type: WebFileItemType.File }) return;
            DispatcherQueue.TryEnqueue(RestoreActiveFileSelection);
        }

        private void ContentSearchMatch_Click(object sender, RoutedEventArgs e) {
            if (sender is FrameworkElement { Tag: WebContentSearchMatch match }) {
                ContentMatchOpenRequested?.Invoke(this, match);
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
        private void FileTreeView_DragItemsStarting(TreeView sender, TreeViewDragItemsStartingEventArgs args) {
            // 源头校验：项目文件、占位节点、磁盘缺失的条目禁止拖拽，直接取消整个拖拽
            if (args.Items.OfType<WebFileItem>().Any(item => !_viewModel.CanDragItem(item))) {
                args.Cancel = true;
                DispatcherQueue.TryEnqueue(RestoreActiveFileSelection);
            }
        }

        /// <summary>
        /// 处理 TreeView 内部条目拖拽的移动逻辑
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void FileTreeView_DragItemsCompleted(TreeView sender, TreeViewDragItemsCompletedEventArgs args) {
            if (args.DropResult == DataPackageOperation.None) {
                RestoreActiveFileSelection();
                return;
            }

            var draggedItems = args.Items.OfType<WebFileItem>().Distinct().ToArray();
            if (draggedItems.Length == 0) return;
            var activeItem = !string.IsNullOrEmpty(_activeFilePath)
                ? _viewModel.FindItem(_activeFilePath)
                : null;
            var oldActiveFilePath = _activeFilePath;

            // 原生拖拽通过节点向量回写已同步数据源（条目已从原集合移除、追加到落点集合），
            // 这里只做数据层的最终落位（跨目录移动或同目录重新排序），
            // 集合变更事件会驱动 TreeView 自动重建对应节点，无需手工操作视觉节点。
            // NewParentItem 为 null 表示落点在根层级（同层重排），按项目根目录处理。
            _viewModel.MoveItems(draggedItems, args.NewParentItem as WebFileItem);

            if (activeItem != null) {
                _activeFilePath = activeItem.FilePath;
                if (!string.IsNullOrEmpty(oldActiveFilePath)
                    && !string.Equals(oldActiveFilePath, _activeFilePath, StringComparison.OrdinalIgnoreCase)) {
                    ActiveFileMoved?.Invoke(
                        this,
                        new WebFileMovedEventArgs(oldActiveFilePath, _activeFilePath));
                }
            }
            RestoreActiveFileSelection();
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

        private async void CutMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;
            await CutCommandAsync();
        }

        private async void CopyMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;
            await CopyCommandAsync();
        }

        private async void PasteMenuItem_Click(object sender, RoutedEventArgs e) {
            var target = GetMenuItemTarget(sender);
            if (target == null) return;

            await PasteCommandAsync();
        }

        private async Task PasteToAsync(WebFileItem target) {
            if (!CanPasteTo(target)) return;

            DataPackageView clipboardData;
            try {
                clipboardData = Clipboard.GetContent();
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WebFileTreeControl>().Error("Failed to read system clipboard.", ex);
                return;
            }

            if (clipboardData.Contains(_systemClipboardOwnerFormat)) {
                var wasCut = _viewModel.HasCutItem;
                if (_viewModel.PasteTo(target)) {
                    clipboardData.ReportOperationCompleted(wasCut ? DataPackageOperation.Move : DataPackageOperation.Copy);
                    if (wasCut) Clipboard.Clear();
                }
                return;
            }

            if (!clipboardData.Contains(StandardDataFormats.StorageItems)) return;

            try {
                var storageItems = await clipboardData.GetStorageItemsAsync();
                var move = clipboardData.RequestedOperation == DataPackageOperation.Move;
                var allSucceeded = true;
                foreach (var storageItem in storageItems) {
                    if (storageItem is not StorageFile and not StorageFolder) continue;
                    allSucceeded &= await _viewModel.TransferExternalAsync(storageItem.Path, target.FilePath, move);
                }
                if (allSucceeded) {
                    clipboardData.ReportOperationCompleted(move ? DataPackageOperation.Move : DataPackageOperation.Copy);
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WebFileTreeControl>().Error("Failed to paste system clipboard items.", ex);
            }
        }

        private async Task PublishStorageItemToClipboardAsync(WebFileItem item, DataPackageOperation operation) {
            try {
                IStorageItem storageItem = item.Type == WebFileItemType.Folder
                    ? await StorageFolder.GetFolderFromPathAsync(item.FilePath)
                    : await StorageFile.GetFileFromPathAsync(item.FilePath);

                var package = new DataPackage { RequestedOperation = operation };
                package.SetStorageItems([storageItem], readOnly: false);
                package.SetData(_systemClipboardOwnerFormat, item.FilePath);
                Clipboard.SetContent(package);
                Clipboard.Flush();
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WebFileTreeControl>().Error($"Failed to publish clipboard item: {item.FilePath}", ex);
            }
        }

        private bool IsOwnedSystemClipboard() {
            try {
                return Clipboard.GetContent().Contains(_systemClipboardOwnerFormat);
            }
            catch {
                return false;
            }
        }

        private static bool HasSystemStorageItems() {
            try {
                return Clipboard.GetContent().Contains(StandardDataFormats.StorageItems);
            }
            catch {
                return false;
            }
        }

        private void CopyPathMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;
            CopyPathCommand();
        }

        private void CopyRelativePathMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = GetMenuItemTarget(sender);
            if (item == null) return;
            CopyRelativePathCommand();
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
            RenameCommand();
        }

        private void BeginRename(WebFileItem item) {
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
                var windowHost = FindAncestor<ArcWindowHost>(textBox);
                _viewModel.CancelRename();
                windowHost?.FocusKeyboardSink();
            }
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject {
            while (current != null) {
                if (current is T result) return result;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
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
            DeleteCommand();
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
            SetContextMenuTarget(folderItem);

            var existsOnDisk = folderItem.ExistsOnDisk;
            var isProjectFile = _viewModel.IsProjectFileItem(folderItem);

            foreach (var item in menuFlyout.Items) {
                switch (item) {
                    case MenuFlyoutItem { Name: "folderCopyPathMenuItem" or "folderCopyRelativePathMenuItem" or "folderRevealMenuItem" } menuItem:
                        SetVisible(menuItem, isProjectFile || existsOnDisk);
                        break;
                    case MenuFlyoutItem { Name: "pasteMenuItem", Tag: WebFileItem pasteTarget } menuItem:
                        SetVisible(menuItem, !isProjectFile && existsOnDisk);
                        menuItem.IsEnabled = CanPasteTo(pasteTarget);
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
            SetContextMenuTarget(fileItem);

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

        private void ContextMenuFlyout_Closed(object sender, object e) {
            SetContextMenuTarget(null);
        }

        private void SetContextMenuTarget(WebFileItem? item) {
            if (ReferenceEquals(_contextMenuItem, item)) return;
            if (_contextMenuItem != null) {
                _contextMenuItem.IsContextMenuTarget = false;
            }
            _contextMenuItem = item;
            if (_contextMenuItem != null) {
                _contextMenuItem.IsContextMenuTarget = true;
            }
            CommandStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private static WebFileItem? ResolvePasteTarget(WebFileItem? item) {
            return item?.Type == WebFileItemType.Folder ? item : item?.Parent;
        }

        private bool CanPasteTo(WebFileItem? target) {
            return target != null
                && target.ExistsOnDisk
                && !_viewModel.IsProjectFileItem(target)
                && (_viewModel.CanPasteTo(target) || HasSystemStorageItems());
        }

        private WebFileItem? GetCommandTarget() {
            return _contextMenuItem
                ?? fileTreeView.SelectedItem as WebFileItem
                ?? _activeFileItem;
        }

        private bool CanModify(WebFileItem? item) {
            return item != null && item.ExistsOnDisk && !_viewModel.IsProjectFileItem(item);
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
        private readonly ObservableCollection<WebFileItem> _stickyFolders = [];
        private string? _activeFilePath;
        private WebFileItem? _activeFileItem;
        private WebFileItem? _contextMenuItem;
        private readonly string _systemClipboardOwnerFormat = $"VirtualPaper.WebBackdrop.FileTree.{Guid.NewGuid():N}";
        private ScrollViewer? _fileTreeScrollViewer;
        private ScrollBar? _fileTreeVerticalScrollBar;
        private ScrollBar? _contentSearchVerticalScrollBar;
        private long? _fileTreeScrollBarIndicatorToken;
        private long? _contentSearchScrollBarIndicatorToken;
        private bool _isRestoringSelection;
        private bool _isPointerOverLeftPanel;
        private bool _stickyFoldersUpdateQueued;
        private const int MaxSelectionRealizationAttempts = 8;
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
