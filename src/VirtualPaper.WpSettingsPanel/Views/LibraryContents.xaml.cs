using System;
using System.Diagnostics;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UIComponent.Context;
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
        public override ArcPageContext Context { get; set; }
        public override Type PageType => typeof(LibraryContents);

        public LibraryContents() {
            this.InitializeComponent();
            _viewModel = ObjectProvider.GetRequiredService<LibraryContentsViewModel>();
            this.DataContext = _viewModel;
            Context = new ArcPageContext(this);            
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e) {
            if (!_isColdLaunch) return;
            _isColdLaunch = false;

            await _viewModel.InitContentAsync();
            _viewModel.RefreshWpTitleForeground();
        }

        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e) {
            // TODO 兜底图片
            ArcLog.GetLogger<LibraryContents>().Error($"RImage loading failed: {e.ErrorMessage}");
        }

        private async void WallpapersLibView_ItemClick(object sender, ItemClickEventArgs e) {
            if (e.ClickedItem is IWpBasicData data) {
                await _viewModel.PreviewAsync(data);
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
                        await _viewModel.PreviewAsync(data);
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
                GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
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
            visual.CenterPoint = new Vector3(
                (float)presenter.ActualWidth * 0.5f,
                (float)presenter.ActualHeight * 0.5f,
                0f);
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

        private readonly LibraryContentsViewModel _viewModel;
        private bool _isColdLaunch = true;

        private void WallpaperLibScrollViewer_ViewChanged(ScrollView sender, object args) {
            if (sender == null) return;

            double verticalOffset = sender.VerticalOffset;
            double maxVerticalOffset = sender.ScrollableHeight;
            double threshold = 100;

            if (maxVerticalOffset - verticalOffset <= threshold) {
                if (_viewModel.LibLoadingStatus != LoadingStatus.Changing) {
                    _viewModel.LoadMoreAsync();
                }
            }
        }
    }

    sealed record ScaleAnimationContext(Visual Visual, Vector3KeyFrameAnimation ScaleToNormal, Vector3KeyFrameAnimation ScaleToHover);
}
