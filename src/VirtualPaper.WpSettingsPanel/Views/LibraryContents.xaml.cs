using System;
using System.Diagnostics;
using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.WpSettingsPanel.ViewModels;
using Windows.ApplicationModel.DataTransfer;

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
        }

        private void LibraryContents_Unloaded(object sender, RoutedEventArgs e) {
            this.Unloaded -= LibraryContents_Unloaded;
            _viewModel.ItemDeleted -= ViewModel_ItemDeleted;
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
            _viewModel.RefreshWpTitleForeground();
        }

        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e) {
            // TODO 兜底图片
            ArcLog.GetLogger<LibraryContents>().Error($"RImage loading failed: {e.ErrorMessage}");
        }

        private async void WallpapersLibView_ItemClick(object sender, ItemClickEventArgs e) {
            if (e.ClickedItem is IWpBasicData data) {
                var ctx = ArcPageContextManager.GetContext<WpSettings>();
                await _viewModel.PreviewAsync(data, ctx);
            }
        }

        private async void ContextMenu_Click(object sender, RoutedEventArgs e) {
            try {
                if (((FrameworkElement)sender).DataContext is not IWpBasicData data)
                    return;

                var selectedMeun = (MenuFlyoutItem)sender;
                string? name = selectedMeun.Tag.ToString();
                switch (name) {
                    case "Details":
                        _viewModel.ShowDetail(data);
                        break;
                    case "UpdateConfig":
                        await _viewModel.UpdateAsync(data);
                        break;
                    case "Edit":
                        _viewModel.ShowEdit(data);
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
                    case "RemoveFromLib":
                        await _viewModel.DeleteAsync(data);
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

        private async void WallpapersLibView_Drop(object sender, DragEventArgs e) {
            if (e.DataView.Contains(StandardDataFormats.StorageItems)) {
                var items = await e.DataView.GetStorageItemsAsync();
                await _viewModel.DropFilesAsync(items);
            }
        }

        private void WallpapersLibView_PreviewKeyDown(object sender, KeyRoutedEventArgs e) {
            e.Handled = true;
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
            var context = new ScaleAnimationContext(visual, CreateScaleAnim(1.0f), CreateScaleAnim(0.9f));

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
        private const double _scrollThreshold = 200;
    }

    sealed record ScaleAnimationContext(Visual Visual, Vector3KeyFrameAnimation ScaleToNormal, Vector3KeyFrameAnimation ScaleToHover);
}
