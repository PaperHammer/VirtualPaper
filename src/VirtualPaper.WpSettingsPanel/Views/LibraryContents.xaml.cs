using System;
using System.Diagnostics;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.WpSettingsPanel.ViewModels;
using Windows.ApplicationModel.DataTransfer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.WpSettingsPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LibraryContents : Page {
        public LibraryContents() {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (this._wpSettingsPanel == null) {
                this._wpSettingsPanel = e.Parameter as IWpSettingsPanel;

                _compositor = this._wpSettingsPanel.GetCompositor() as Compositor;
                _viewModel = this._wpSettingsPanel.GetRequiredService<LibraryContentsViewModel>(lifetimeForParams: ObjectLifetime.Singleton);
                this.DataContext = _viewModel;
            }
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e) {
            await _viewModel.InitContentAsync();
        }

        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e) {
            this._wpSettingsPanel.Log(LogType.Error, $"RImage loading failed: {e.ErrorMessage}");
        }

        private async void SingleClickAction(IWpBasicData data) {
            await _viewModel.PreviewAsync(data);
        }

        private void ItemsViewer_PointerPressed(object sender, PointerRoutedEventArgs e) {
            var dataContext = ((FrameworkElement)e.OriginalSource).DataContext;
            if (dataContext is not IWpBasicData data) return;

            // ref: https://learn.microsoft.com/zh-cn/uwp/api/windows.ui.xaml.uielement.pointerpressed?view=winrt-26100
            Pointer ptr = e.Pointer;
            if (ptr.PointerDeviceType == PointerDeviceType.Mouse) {
                PointerPoint ptrPt = e.GetCurrentPoint(ItemsViewer);
                if (!ptrPt.Properties.IsLeftButtonPressed) return;

                SingleClickAction(data);
            }
        }

        private void ItemsView_RightTapped(object sender, RightTappedRoutedEventArgs e) {
            var dataContext = ((FrameworkElement)e.OriginalSource).DataContext;
            _data = dataContext as IWpBasicData;
            var itemsView = (ItemsView)sender;

            if (_data == null) {
                //Hide()方法可能无效是因为MenuFlyout是由ContextFlyout属性触发
                //ItemsViewMenu.Hide();
                //var itemsView = (ItemsView)sender;
                itemsView.ContextFlyout = null;
            }
            else {
                itemsView.ContextFlyout = ItemsViewMenu;
            }

            e.Handled = true;
        }

        private async void ContextMenu_Click(object sender, RoutedEventArgs e) {
            if (_data == null) return;

            try {
                var selectedMeun = (MenuFlyoutItem)sender;
                string name = selectedMeun.Name;

                switch (name) {
                    case "Details":
                        await _viewModel.DetailedInfoAsync(_data);
                        break;
                    case "UpdateConfig":
                        await _viewModel.UpdateAsync(_data);
                        break;
                    case "Edit":
                        await _viewModel.EditInfoAsync(_data);
                        break;
                    case "Preview":
                        await _viewModel.PreviewAsync(_data);
                        break;
                    case "Apply":
                        await _viewModel.ApplyAsync(_data);
                        break;
                    case "LockBackground":
                        await _viewModel.ApplyToLockBGAsync(_data);
                        break;
                    case "ShowOnDisk":
                        Process.Start("Explorer", "/select," + _data.FilePath);
                        break;
                    case "DeleteFromDisk":
                        await _viewModel.DeleteAsync(_data);
                        break;
                }
            }
            catch (Exception ex) {
                this._wpSettingsPanel.GetNotify().ShowExp(ex);
                this._wpSettingsPanel.Log(LogType.Error, ex);
            }
        }

        private void ItemsView_DragOver(object sender, DragEventArgs e) {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void ItemsView_Drop(object sender, DragEventArgs e) {
            if (e.DataView.Contains(StandardDataFormats.StorageItems)) {
                var items = await e.DataView.GetStorageItemsAsync();
                await _viewModel.DropFilesAsync(items);
            }
            e.Handled = true;
        }

        private void ItemGrid_PointerEntered(object sender, PointerRoutedEventArgs e) {
            CreateOrUpdateSpringAnimation(1.04f);
            (sender as UIElement).StartAnimation(_springAnimation);
        }

        private void ItemGrid_PointerExited(object sender, PointerRoutedEventArgs e) {
            CreateOrUpdateSpringAnimation(1.0f);
            (sender as UIElement).StartAnimation(_springAnimation);
        }

        private void CreateOrUpdateSpringAnimation(float finalValue) {
            if (_springAnimation == null) {
                _springAnimation = _compositor.CreateSpringVector3Animation();
                _springAnimation.Target = "Scale";
                _springAnimation.DampingRatio = 0.4f;
                _springAnimation.Period = TimeSpan.FromMilliseconds(50);
            }

            _springAnimation.FinalValue = new Vector3(finalValue);
        }

        private void ItemsViewer_PreviewKeyDown(object sender, KeyRoutedEventArgs e) {
            e.Handled = true;
        }

        private IWpSettingsPanel _wpSettingsPanel;
        private LibraryContentsViewModel _viewModel;
        private IWpBasicData _data;
        private Compositor _compositor;
        private SpringVector3NaturalMotionAnimation _springAnimation;
    }
}
