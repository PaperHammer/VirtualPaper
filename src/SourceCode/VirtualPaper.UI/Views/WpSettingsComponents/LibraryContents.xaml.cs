using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NLog;
using System;
using System.Diagnostics;
using System.Numerics;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Models.WallpaperMetaData;
using VirtualPaper.UI.ViewModels.WpSettingsComponents;
using Windows.ApplicationModel.DataTransfer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI.Views.WpSettingsComponents
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LibraryContents : Page
    {
        public LibraryContents()
        {
            this.InitializeComponent();

            uint interval = Native.GetDoubleClickTime();
            _doubleClickMaxTime = TimeSpan.FromMilliseconds(interval);
            _clickTimer = new DispatcherTimer();
            _clickTimer.Interval = TimeSpan.FromMilliseconds(interval);
            _clickTimer.Tick += ClickTimer_Tick;

            _viewModel = App.Services.GetRequiredService<LibraryContentsViewModel>();
            this.DataContext = _viewModel;
        }

        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            _logger.Error($"Image loading failed: {e.ErrorMessage}");
        }

        // ref: https://learn.microsoft.com/zh-cn/dotnet/desktop/winforms/input-mouse/how-to-distinguish-between-clicks-and-double-clicks?view=netdesktop-8.0
        private async void SingleClickAction()
        {
            await _viewModel.DetailedInfoAsync((IMetaData)ItemsViewer.SelectedItem);
        }

        private async void DoubleClickAction(MetaData _pointerPressedItem)
        {
            await _viewModel.PreviewAsync(_pointerPressedItem);
        }

        private void ClickTimer_Tick(object sender, object e)
        {
            _inDoubleClick = false;
            _clickTimer.Stop();

            SingleClickAction();
        }

        private void ItemsViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var dataContext = ((FrameworkElement)e.OriginalSource).DataContext;
            if (dataContext is not MetaData _pointerPressedItem) return;

            // ref: https://learn.microsoft.com/zh-cn/uwp/api/windows.ui.xaml.uielement.pointerpressed?view=winrt-26100
            Pointer ptr = e.Pointer;
            if (ptr.PointerDeviceType == PointerDeviceType.Mouse)
            {                
                PointerPoint ptrPt = e.GetCurrentPoint(ItemsViewer);
                if (!ptrPt.Properties.IsLeftButtonPressed) return;

                if (_inDoubleClick)
                {
                    _inDoubleClick = false;

                    TimeSpan length = DateTime.Now - _lastClick;

                    // If double click is valid, respond
                    if (length < _doubleClickMaxTime)
                    {
                        _clickTimer.Stop();
                        DoubleClickAction(_pointerPressedItem);
                    }

                    return;
                }

                // Double click was invalid, restart 
                _clickTimer.Stop();
                _clickTimer.Start();
                _lastClick = DateTime.Now;
                _inDoubleClick = true;
            }
        }

        private void ItemsView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var dataContext = ((FrameworkElement)e.OriginalSource).DataContext;
            _rightTrappedItem = dataContext as MetaData;
            var itemsView = (ItemsView)sender;

            if (_rightTrappedItem == null)
            {
                //Hide()方法可能无效是因为MenuFlyout是由ContextFlyout属性触发
                //ItemsViewMenu.Hide();
                //var itemsView = (ItemsView)sender;
                itemsView.ContextFlyout = null;
            }
            else
            {
                itemsView.ContextFlyout = ItemsViewMenu;
            }

            e.Handled = true;
        }

        private async void ContextMenu_Click(object sender, RoutedEventArgs e)
        {
            if (_rightTrappedItem == null)
            {
                _viewModel.ShowErr();
                return;
            }

            try
            {
                var selectedMeun = (MenuFlyoutItem)sender;
                string name = selectedMeun.Name;

                switch (name)
                {
                    case "DetailedInfo":
                        await _viewModel.DetailedInfoAsync(_rightTrappedItem);
                        break;
                    case "EditInfo":
                        await _viewModel.EditInfoAsync(_rightTrappedItem);
                        break;
                    case "Preview":
                        await _viewModel.PreviewAsync(_rightTrappedItem);
                        break;
                    case "Import":
                        await _viewModel.ImportAsync(_rightTrappedItem);
                        break;
                    case "Apply":
                        await _viewModel.ApplyAsync(_rightTrappedItem);
                        break;
                    case "LockBackground":
                        await _viewModel.ApplyToLockBGAsync(_rightTrappedItem);
                        break;
                    case "ShowOnDisk":
                        Process.Start("Explorer", "/select," + _rightTrappedItem.FilePath);
                        break;
                    case "Delete":
                        await _viewModel.DeleteAsync(_rightTrappedItem);
                        break;
                }
            }
            catch (Exception ex)
            {
                _viewModel.ShowErr();
                _logger.Error(ex);
            }
        }

        private void ItemsView_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void ItemsView_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                await _viewModel.TryDropFileAsync(items);
            }
            e.Handled = true;
        }

        private void ItemGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            CreateOrUpdateSpringAnimation(1.04f);

            (sender as UIElement).StartAnimation(_springAnimation);
        }

        private void ItemGrid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            CreateOrUpdateSpringAnimation(1.0f);

            (sender as UIElement).StartAnimation(_springAnimation);
        }
       
        private void CreateOrUpdateSpringAnimation(float finalValue)
        {
            if (_springAnimation == null)
            {
                _springAnimation = _compositor.CreateSpringVector3Animation();
                _springAnimation.Target = "Scale";
                _springAnimation.DampingRatio = 0.4f;
                _springAnimation.Period = TimeSpan.FromMilliseconds(50);
            }

            _springAnimation.FinalValue = new Vector3(finalValue);
        }

        private LibraryContentsViewModel _viewModel;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private IMetaData _rightTrappedItem;
        private Compositor _compositor = App.Services.GetRequiredService<MainWindow>().Compositor;
        private SpringVector3NaturalMotionAnimation _springAnimation;

        private bool _inDoubleClick;
        private DateTime _lastClick;
        private TimeSpan _doubleClickMaxTime;
        private DispatcherTimer _clickTimer;
    }
}
