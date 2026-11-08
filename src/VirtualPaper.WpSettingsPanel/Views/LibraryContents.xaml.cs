using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.WpSettingsPanel.ViewModels;
using VirtualPaper.WpSettingsPanel.Utils;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.WpSettingsPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LibraryContents : ArcPage {
        public override Type ArcType => typeof(LibraryContents);

        public LibraryContents() {
            this.InitializeComponent();
            this.Loaded += LibraryContents_Loaded;
            this.Unloaded += LibraryContents_Unloaded;
            _viewModel = AppServiceLocator.Services.GetRequiredService<LibraryContentsViewModel>();
            this.DataContext = _viewModel;
        }

        private void LibraryContents_Loaded(object sender, RoutedEventArgs e) {
            this.DataContext = _viewModel;
            _viewModel.ItemDeleted += ViewModel_ItemDeleted;
            _viewModel.OrganizationChanged += ViewModel_OrganizationChanged;
            LanguageUtil.LanguageUpdated += ViewModel_OrganizationChanged;
            RefreshFavoriteButtons();
            UpdateWallpaperTileSize();
        }

        private void LibraryContents_Unloaded(object sender, RoutedEventArgs e) {
            _viewModel.ItemDeleted -= ViewModel_ItemDeleted;
            _viewModel.OrganizationChanged -= ViewModel_OrganizationChanged;
            LanguageUtil.LanguageUpdated -= ViewModel_OrganizationChanged;
            this.DataContext = null;           
        }

        private void ViewModel_ItemDeleted(object? sender, EventArgs e) {
            if (wallpaperLibScrollViewer == null) return;

            // ScrollableHeight = 内容总高度 - 视口高度
            // 若剩余可滚动距离 <= threshold，说明内容不够多，需要补充加载
            if (wallpaperLibScrollViewer.ScrollableHeight <= _scrollThreshold) {
                if (_viewModel.LibLoadingStatus != LoadingStatus.Changing) {
                    _viewModel.LoadMoreAsync();
                }
            }
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e) {            
            await _viewModel.InitContentAsync();
        }

        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e) {
            // 图片加载失败（文件缺失/损坏）时隐藏图片，显示原生占位缩略图
            if (sender is Image image) {
                image.Visibility = Visibility.Collapsed;
                if (image.Parent is FrameworkElement parent
                    && parent.FindName("NoPreviewPlaceholder") is UIElement placeholder) {
                    placeholder.Visibility = Visibility.Visible;
                }
            }

            ArcLog.GetLogger<LibraryContents>().Warn($"Image loading failed: {e.ErrorMessage}");
        }

        private async void WallpapersLibView_ItemClick(object sender, ItemClickEventArgs e) {
            if (wallpapersLibView.SelectionMode == ListViewSelectionMode.Multiple) {
                return;
            }

            if (e.ClickedItem is IWpBasicData data) {
                var ctx = ArcPageContextManager.GetContext<WpSettings>();
                await _viewModel.PreviewAsync(data, ctx);
            }
        }

        private void WallpapersLibView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args) {
            var data = args.InRecycleQueue ? null : args.Item as IWpBasicData;
            AutomationProperties.SetAutomationId(args.ItemContainer, data == null ? string.Empty : "Library.Item." + data.WallpaperUid);
            AutomationProperties.SetName(args.ItemContainer, data == null ? string.Empty :
                string.IsNullOrWhiteSpace(data.Title) ? System.IO.Path.GetFileName(data.FilePath) : data.Title);
        }

        public void EnterSelectionMode() {
            // WinUI 的 ItemClick 模式与原生多选互斥；关闭后点击会立即更新 SelectedItems 和选中视觉态。
            wallpapersLibView.IsItemClickEnabled = false;
            wallpapersLibView.SelectionMode = ListViewSelectionMode.Multiple;
            NotifySelectionChanged();
        }

        public void SelectAllItems() {
            if (wallpapersLibView.SelectionMode == ListViewSelectionMode.Multiple) {
                wallpapersLibView.SelectAll();
            }
        }

        public void InvertSelection() {
            if (wallpapersLibView.SelectionMode != ListViewSelectionMode.Multiple) return;

            var selected = wallpapersLibView.SelectedItems.Cast<object>().ToHashSet();
            var toSelect = wallpapersLibView.Items.Cast<object>()
                .Where(item => !selected.Contains(item))
                .ToArray();

            _updatingSelection = true;
            try {
                wallpapersLibView.SelectedItems.Clear();
                foreach (var item in toSelect) {
                    wallpapersLibView.SelectedItems.Add(item);
                }
            }
            finally {
                _updatingSelection = false;
            }

            NotifySelectionChanged();
        }

        public async Task DeleteSelectedItemsAsync() {
            var selectedItems = wallpapersLibView.SelectedItems
                .OfType<IWpBasicData>()
                .ToList();
            if (selectedItems.Count == 0) return;

            await _viewModel.DeleteAsync(selectedItems);
            if (wallpapersLibView.SelectedItems.Count == 0) {
                ExitSelectionMode();
            }
            else {
                NotifySelectionChanged();
            }
        }

        public void ExitSelectionMode() {
            // SelectionMode=None 时修改 SelectedItems 会触发 WinUI 异常，阻断页面导航。
            if (wallpapersLibView.SelectionMode != ListViewSelectionMode.None) {
                wallpapersLibView.SelectedItems.Clear();
                wallpapersLibView.SelectionMode = ListViewSelectionMode.None;
            }
            wallpapersLibView.IsItemClickEnabled = true;
            RefreshFavoriteButtons();
            var ctx = ArcPageContextManager.GetContext<WpSettings>();
            ctx?.GetPageInstance<WpSettings>().UpdateLibrarySelectionState(false, 0);
        }

        private void WallpapersLibView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!_updatingSelection) NotifySelectionChanged();
        }

        private void NotifySelectionChanged() {
            RefreshFavoriteButtons();
            var ctx = ArcPageContextManager.GetContext<WpSettings>();
            ctx?.GetPageInstance<WpSettings>().UpdateLibrarySelectionState(
                wallpapersLibView.SelectionMode == ListViewSelectionMode.Multiple,
                wallpapersLibView.SelectedItems.Count);
        }

        private void WallpaperContextFlyout_Opening(object sender, object e) {
            if (sender is not MenuFlyout flyout) return;
            var favorites = _viewModel.FavoriteActions(_contextItems);
            bool single = _contextItems.Count == 1;
            foreach (var item in flyout.Items.OfType<MenuFlyoutItem>()) {
                string action = item.Tag as string ?? "";
                bool visible = action switch {
                    "AddFavorite" => favorites.CanAdd,
                    "RemoveFavorite" => favorites.CanRemove,
                    "EditTags" => _contextItems.Count > 1,
                    "RemoveFromLib" => _contextItems.Count > 0,
                    "UpdateConfig" => single && _contextItems[0].AppInfo.FileVersion != Constants.CoreField.FileVersion,
                    "LockBackground" => single && _contextItems[0].FType is FileType.FImage or FileType.FGif,
                    _ => single
                };
                item.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                item.IsEnabled = !_isOrganizing;
            }
        }

        private async void ContextMenu_Click(object sender, RoutedEventArgs e) {
            try {
                if (((FrameworkElement)sender).DataContext is not IWpBasicData data)
                    return;

                var selectedMeun = (MenuFlyoutItem)sender;
                string? name = selectedMeun.Tag.ToString();
                if (name is "AddFavorite" or "RemoveFavorite" or "EditTags" or "Edit") {
                    await OrganizeItemsAsync(_contextItems.ToArray(), name);
                    return;
                }
                if (name == "RemoveFromLib") {
                    await _viewModel.DeleteAsync(_contextItems.ToArray());
                    if (wallpapersLibView.SelectionMode == ListViewSelectionMode.Multiple) NotifySelectionChanged();
                    return;
                }
                if (_contextItems.Count != 1) return;

                switch (name) {
                    case "Details":
                        _viewModel.ShowDetail(data);
                        break;
                    case "UpdateConfig":
                        await _viewModel.UpdateAsync(data);
                        break;
                    case "Preview":
                        var ctx = ArcPageContextManager.GetContext<WpSettings>();
                        await _viewModel.PreviewAsync(data, ctx);
                        break;
                    case "Apply":
                        await _viewModel.ApplyAsync(data);
                        break;
                    case "LockBackground":
                        await _viewModel.ApplyToLockBGAsync(data);
                        break;
                    case "ShowOnDisk":
                        Process.Start("Explorer", "/select," + data.FilePath);
                        break;
                }
            }
            catch (Exception ex) {
                GlobalMessageUtil.ShowException(ex);
                ArcLog.GetLogger<LibraryContents>().Error(ex);
            }
        }

        private void WallpapersLibView_DragOver(object sender, DragEventArgs e) {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private void FavoriteButton_Loaded(object sender, RoutedEventArgs e) {
            _favoriteButtons.Add((ToggleButton)sender);
            RefreshFavoriteButtons();
        }

        private void FavoriteButton_Unloaded(object sender, RoutedEventArgs e) => _favoriteButtons.Remove((ToggleButton)sender);

        private void FavoriteButton_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) => RefreshFavoriteButtons();

        private async void FavoriteButton_Click(object sender, RoutedEventArgs e) {
            if (sender is ToggleButton { DataContext: IWpBasicData data }) {
                await OrganizeItemsAsync([data], _viewModel.IsFavorite(data) ? "RemoveFavorite" : "AddFavorite");
                RefreshFavoriteButtons();
            }
        }

        private async void EditInfoButton_Click(object sender, RoutedEventArgs e) {
            if (sender is FrameworkElement { DataContext: IWpBasicData data })
                await OrganizeItemsAsync([data], "Edit");
        }

        private void ViewModel_OrganizationChanged(object? sender, EventArgs e) {
            RefreshFavoriteButtons();
            WallpaperContextFlyout_Opening(Resources["WallpaperContextFlyout"], EventArgs.Empty);
        }

        private void RefreshFavoriteButtons() {
            foreach (var button in _favoriteButtons.ToArray()) {
                if (button.DataContext is not IWpBasicData data) continue;
                bool favorite = _viewModel.IsFavorite(data);
                button.IsChecked = favorite;
                if (VisualTreeHelper.GetParent(button) is not StackPanel actions) continue;
                var card = VisualTreeHelper.GetParent(actions);
                bool selecting = wallpapersLibView.SelectionMode == ListViewSelectionMode.Multiple;
                bool show = !selecting && ReferenceEquals(card, _hoveredCard);
                actions.Visibility = selecting ? Visibility.Collapsed : Visibility.Visible;
                AnimateCardActions(actions, show);
                foreach (var control in actions.Children.OfType<Control>()) {
                    control.IsEnabled = show && !_isOrganizing;
                    control.IsTabStop = show;
                }
                if (button.Content is FontIcon icon) icon.Glyph = favorite ? "\uE735" : "\uE734";
                string label = LanguageUtil.GetI18n(favorite ? "WpLib_RemoveFavorite" : "WpLib_AddFavorite");
                ToolTipService.SetToolTip(button, label);
                AutomationProperties.SetName(button, label);
                AutomationProperties.SetAutomationId(button, "Library.Favorite." + data.WallpaperUid);
                if (actions.Children.OfType<Button>().FirstOrDefault() is Button edit) {
                    string editLabel = LanguageUtil.GetI18n("WpLib_EditInfo");
                    ToolTipService.SetToolTip(edit, editLabel);
                    AutomationProperties.SetName(edit, editLabel);
                    AutomationProperties.SetAutomationId(edit, "Library.Edit." + data.WallpaperUid);
                }
            }
        }

        private void WallpaperCard_PointerEntered(object sender, PointerRoutedEventArgs args) {
            _hoveredCard = sender as FrameworkElement;
            RefreshFavoriteButtons();
        }

        private void WallpaperCard_PointerExited(object sender, PointerRoutedEventArgs args) {
            if (sender is not FrameworkElement card || !ReferenceEquals(card, _hoveredCard)) return;
            var point = args.GetCurrentPoint(card).Position;
            // Exiting a descendant must not hide the corner while still inside the card.
            if (point.X >= 0 && point.Y >= 0 && point.X < card.ActualWidth && point.Y < card.ActualHeight) return;
            _hoveredCard = null;
            RefreshFavoriteButtons();
        }

        private void WallpaperCard_PointerCanceled(object sender, PointerRoutedEventArgs args) {
            if (ReferenceEquals(sender, _hoveredCard)) _hoveredCard = null;
            RefreshFavoriteButtons();
        }

        private void WallpaperCard_Unloaded(object sender, RoutedEventArgs args) {
            if (ReferenceEquals(sender, _hoveredCard)) _hoveredCard = null;
        }

        private void WallpaperCard_SizeChanged(object sender, SizeChangedEventArgs args) {
            if (sender is FrameworkElement card)
                card.Clip = new RectangleGeometry { Rect = new Rect(0, 0, args.NewSize.Width, args.NewSize.Height) };
        }

        private void CardActions_Loaded(object sender, RoutedEventArgs args) {
            var actions = (FrameworkElement)sender;
            (actions.Tag as CardActionsState)?.Animation?.Stop();
            actions.Opacity = 0;
            ((TranslateTransform)actions.RenderTransform).X = 48;
            actions.Tag = new CardActionsState(false, null);
            RefreshFavoriteButtons();
        }

        private void CardActions_Unloaded(object sender, RoutedEventArgs args) {
            var actions = (FrameworkElement)sender;
            (actions.Tag as CardActionsState)?.Animation?.Stop();
            actions.Tag = null;
            actions.Opacity = 0;
            ((TranslateTransform)actions.RenderTransform).X = 48;
        }

        private static void AnimateCardActions(FrameworkElement actions, bool show) {
            actions.IsHitTestVisible = show;
            actions.Tag ??= new CardActionsState(false, null);
            if (actions.Tag is CardActionsState { Visible: var previous } && previous == show) return;
            var transform = (TranslateTransform)actions.RenderTransform;
            double x = transform.X, opacity = actions.Opacity;
            (actions.Tag as CardActionsState)?.Animation?.Stop();
            var duration = new Duration(TimeSpan.FromMilliseconds(show ? 160 : 120));
            var slide = new DoubleAnimation { From = x, To = show ? 0 : 48, Duration = duration,
                EasingFunction = new CubicEase { EasingMode = show ? EasingMode.EaseOut : EasingMode.EaseIn } };
            var fade = new DoubleAnimation { From = opacity, To = show ? 1 : 0, Duration = duration };
            Storyboard.SetTarget(slide, transform);
            Storyboard.SetTargetProperty(slide, "X");
            Storyboard.SetTarget(fade, actions);
            Storyboard.SetTargetProperty(fade, "Opacity");
            var animation = new Storyboard();
            animation.Children.Add(slide);
            animation.Children.Add(fade);
            actions.Tag = new CardActionsState(show, animation);
            animation.Begin();
        }

        public Task OrganizeSelectedAsync(string action) =>
            OrganizeItemsAsync(wallpapersLibView.SelectedItems.OfType<IWpBasicData>().ToArray(), action);

        public void UpdateOrganizationMenu(MenuFlyout flyout) {
            var favorites = _viewModel.FavoriteActions(wallpapersLibView.SelectedItems.OfType<IWpBasicData>());
            foreach (var item in flyout.Items.OfType<MenuFlyoutItem>()) {
                item.Visibility = (item.Tag as string) switch {
                    "AddFavorite" => favorites.CanAdd ? Visibility.Visible : Visibility.Collapsed,
                    "RemoveFavorite" => favorites.CanRemove ? Visibility.Visible : Visibility.Collapsed,
                    _ => Visibility.Visible
                };
                item.IsEnabled = !_isOrganizing && wallpapersLibView.SelectedItems.Count > 0;
            }
        }

        private async Task OrganizeItemsAsync(IReadOnlyList<IWpBasicData> items, string action) {
            if (_isOrganizing || items.Count == 0) return;
            _isOrganizing = true;
            RefreshFavoriteButtons();
            try {
                if (action is "AddFavorite" or "RemoveFavorite") {
                    await _viewModel.OrganizeAsync(items, favorite: action == "AddFavorite");
                    return;
                }
                if (items.Count == 1) {
                    await EditInfoAsync(items[0]);
                    return;
                }
                var tags = new TextBox {
                    Header = LanguageUtil.GetI18n("WpLib_YourTags"),
                    PlaceholderText = LanguageUtil.GetI18n("WpLib_TagsPlaceholder"),
                    AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 140
                };
                var mode = new ComboBox {
                    Header = LanguageUtil.GetI18n("WpLib_TagMode"),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    ItemsSource = new[] { LanguageUtil.GetI18n("WpLib_TagsAdd"), LanguageUtil.GetI18n("WpLib_TagsRemove"), LanguageUtil.GetI18n("WpLib_TagsReplace") },
                    SelectedIndex = 0
                };
                var content = new StackPanel { Spacing = 12, MinWidth = 280, MaxWidth = 420 };
                content.Children.Add(new TextBlock {
                    Text = LanguageUtil.GetI18n("WpLib_TagsExplain"), TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.75
                });
                content.Children.Add(mode);
                content.Children.Add(tags);
                var dialog = GlobalDialogUtils.CreateDialog(content, LanguageUtil.GetI18n("WpLib_EditTags"), LanguageUtil.GetI18n(Constants.I18n.Text_Save));
                if (dialog == null) return;
                dialog.CloseButtonText = LanguageUtil.GetI18n(Constants.I18n.Text_Cancel);
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
                var update = mode.SelectedIndex switch {
                    1 => WallpaperTagUpdate.Remove, 2 => WallpaperTagUpdate.Replace, _ => WallpaperTagUpdate.Add
                };
                await _viewModel.OrganizeAsync(items, tags: [tags.Text], mode: update);
            }
            catch (Exception ex) { ArcLog.GetLogger<LibraryContents>().Error(ex); GlobalMessageUtil.ShowException(ex); }
            finally { _isOrganizing = false; RefreshFavoriteButtons(); }
        }

        private async Task EditInfoAsync(IWpBasicData data) {
            var editor = new WallpaperDetailsEditor(data, _viewModel.GetOrganization(data.WallpaperUid));
            var dialog = GlobalDialogUtils.CreateDialog(editor, LanguageUtil.GetI18n("WpLib_EditInfo"), LanguageUtil.GetI18n(Constants.I18n.Text_Save));
            if (dialog == null) return;
            dialog.CloseButtonText = LanguageUtil.GetI18n(Constants.I18n.Text_Cancel);
            dialog.DialogWidth = 540;
            dialog.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            bool saving = false;
            dialog.Closing += (_, args) => { if (saving) args.Cancel = true; };
            dialog.PrimaryButtonClick += async (_, args) => {
                var deferral = args.GetDeferral();
                saving = true;
                editor.IsEnabled = dialog.IsPrimaryButtonEnabled = false;
                try { await _viewModel.SaveDetailsAsync(data, editor.ReadEdit()); }
                catch (Exception ex) {
                    args.Cancel = true;
                    ArcLog.GetLogger<LibraryContents>().Error(ex);
                    GlobalMessageUtil.ShowException(ex);
                }
                finally {
                    saving = false;
                    editor.IsEnabled = dialog.IsPrimaryButtonEnabled = true;
                    deferral.Complete();
                }
            };
            await dialog.ShowAsync();
        }

        private void WallpaperItem_ContextRequested(UIElement sender, ContextRequestedEventArgs args) {
            if (sender is not FrameworkElement { DataContext: IWpBasicData data } target) return;
            Point? position = args.TryGetPosition(target, out var point) ? point : null;
            ShowWallpaperContextMenu(target, data, position);
            args.Handled = true;
        }

        private void ShowWallpaperContextMenu(FrameworkElement target, IWpBasicData data, Point? position = null) {
            // A shared flyout has no reliable inherited item DataContext. Capture the
            // same targets for menu visibility and clicks, for both mouse and keyboard.
            _contextItems = LibraryContentsViewModel.ContextItems(data,
                wallpapersLibView.SelectionMode == ListViewSelectionMode.Multiple
                    ? wallpapersLibView.SelectedItems.OfType<IWpBasicData>() : []);
            var flyout = (MenuFlyout)Resources["WallpaperContextFlyout"];
            foreach (var item in flyout.Items.OfType<MenuFlyoutItem>()) item.DataContext = data;
            flyout.ShowAt(target, new FlyoutShowOptions { Position = position });
        }

        private async void WallpapersLibView_Drop(object sender, DragEventArgs e) {
            if (e.DataView.Contains(StandardDataFormats.StorageItems)) {
                var items = await e.DataView.GetStorageItemsAsync();
                await _viewModel.DropFilesAsync(items);
            }
        }

        private void WallpapersLibView_PreviewKeyDown(object sender, KeyRoutedEventArgs e) {
            if (e.Key == Windows.System.VirtualKey.Tab) return;
            if (FocusManager.GetFocusedElement(XamlRoot) is ButtonBase action
                && VisualTreeHelper.GetParent(action) is StackPanel { Name: "CardActions" }) return;
            e.Handled = e.Key is not (Windows.System.VirtualKey.F10 or Windows.System.VirtualKey.Application);
        }

        private void WallpapersLibView_ContextRequested(UIElement sender, ContextRequestedEventArgs args) {
            if (args.Handled) return;
            // Keyboard requests may originate at the container rather than its tile.
            if (args.TryGetPosition(wallpapersLibView, out _)) return;
            if (FocusManager.GetFocusedElement(XamlRoot) is not DependencyObject focused) return;
            while (focused is not GridViewItem && focused != wallpapersLibView) {
                focused = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(focused);
                if (focused == null) return;
            }
            var container = focused as GridViewItem;
            if (container?.Content is not IWpBasicData data) return;
            ShowWallpaperContextMenu(container, data);
            args.Handled = true;
        }

        private void WallpapersLibView_SizeChanged(object sender, SizeChangedEventArgs e) {
            UpdateWallpaperTileSize();
        }

        private void UpdateWallpaperTileSize() {
            if (wallpapersLibView.ItemsPanelRoot is not ItemsWrapGrid panel || wallpapersLibView.ActualWidth <= 0) return;

            const double gap = 12;
            // 按约 320px 的卡片宽度选择列数，再平分整行；向下取整避免布局舍入导致换行。
            int columns = Math.Max(1, (int)Math.Round(wallpapersLibView.ActualWidth / (320 + gap), MidpointRounding.AwayFromZero));
            double cellWidth = Math.Floor(wallpapersLibView.ActualWidth / columns);
            panel.ItemWidth = cellWidth;
            panel.ItemHeight = Math.Round(Math.Max(1, cellWidth - gap) * 5 / 8) + gap;
        }

        private void GridViewItem_PointerEntered(object sender, PointerRoutedEventArgs e) {
            AnimateScale(sender, hover: true);
        }

        private void GridViewItem_PointerExited(object sender, PointerRoutedEventArgs e) {
            AnimateScale(sender, hover: false);
        }

        private void GridViewItem_PointerCanceled(object sender, PointerRoutedEventArgs e) {
            AnimateScale(sender, hover: false);
        }

        private void GridViewItem_Loaded(object sender, RoutedEventArgs e) {
            if (sender is not Grid root || root.Children.Count == 0)
                return;

            var presenter = (FrameworkElement)root.Children[0];
            var visual = ElementCompositionPreview.GetElementVisual(presenter);

            // Loaded 时新加载的 item 布局尚未完成，ActualWidth/Height 可能为 0
            // 用 SizeChanged 保证尺寸确定后再更新 CenterPoint
            presenter.SizeChanged += (s, _) => {
                var fe = (FrameworkElement)s;
                visual.CenterPoint = new Vector3(
                    (float)fe.ActualWidth * 0.5f,
                    (float)fe.ActualHeight * 0.5f,
                    0f);
            };

            // 初始加载时尺寸已知，直接设置；新加载 item 尺寸为 0 时跳过，等 SizeChanged
            if (presenter.ActualWidth > 0 && presenter.ActualHeight > 0) {
                visual.CenterPoint = new Vector3(
                    (float)presenter.ActualWidth * 0.5f,
                    (float)presenter.ActualHeight * 0.5f,
                    0f);
            }

            visual.Scale = Vector3.One;

            var compositor = visual.Compositor;
            var ease = compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.2f, 0.0f),
                new Vector2(0.0f, 1.0f));

            Vector3KeyFrameAnimation CreateScaleAnim(float scale) {
                var anim = compositor.CreateVector3KeyFrameAnimation();
                anim.Duration = TimeSpan.FromMilliseconds(300);
                anim.InsertKeyFrame(1f, new Vector3(scale, scale, 1f), ease);
                return anim;
            }
            var context = new ScaleAnimationContext(visual, CreateScaleAnim(1.0f), CreateScaleAnim(0.98f));

            root.Tag = context;
        }

        private static void AnimateScale(object sender, bool hover) {
            if (sender is not Grid root || root.Tag is not ScaleAnimationContext ctx)
                return;

            var visual = ctx.Visual;
            visual.StartAnimation(nameof(visual.Scale), hover ? ctx.ScaleToHover : ctx.ScaleToNormal);
        }

        private void WallpaperLibScrollViewer_ViewChanged(ScrollView sender, object args) {
            if (sender == null) return;

            double verticalOffset = sender.VerticalOffset;
            double maxVerticalOffset = sender.ScrollableHeight;

            if (maxVerticalOffset - verticalOffset <= _scrollThreshold) {
                if (_viewModel.LibLoadingStatus != LoadingStatus.Changing) {
                    _viewModel.LoadMoreAsync();
                }
            }
        }

        private readonly LibraryContentsViewModel _viewModel;
        private readonly HashSet<ToggleButton> _favoriteButtons = [];
        private FrameworkElement? _hoveredCard;
        private IReadOnlyList<IWpBasicData> _contextItems = [];
        private bool _isOrganizing;
        private const double _scrollThreshold = 200;
        private bool _updatingSelection;
    }

    sealed record ScaleAnimationContext(Visual Visual, Vector3KeyFrameAnimation ScaleToNormal, Vector3KeyFrameAnimation ScaleToHover);
    sealed record CardActionsState(bool Visible, Storyboard? Animation);
}
