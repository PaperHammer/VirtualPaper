using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UI.Utils;
using VirtualPaper.UI.ViewModels;
using WinRT.Interop;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WindowEx
    {
        public MainWindow(
            IUserSettingsClient userSettings)
        {
            _userSettingsClient = userSettings;

            this.InitializeComponent();

            _viewModel = new MainWindowViewModel();
            this.NavView.DataContext = _viewModel;

            string type = _userSettingsClient.Settings.SystemBackdrop.ToString();
            this.SystemBackdrop = type switch
            {
                "Mica" => new MicaBackdrop(),
                "Acrylic" => new DesktopAcrylicBackdrop(),
                _ => default,
            };

            //ref: https://learn.microsoft.com/en-us/windows/apps/develop/title-bar?tabs=wasdk
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = this.AppWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonForegroundColor = ((SolidColorBrush)App.Current.Resources["WindowCaptionForeground"]).Color;

                AppTitleBar.Loaded += AppTitleBar_Loaded;
                AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
                this.Activated += MainWindow_Activated;
            }
            else
            {
                AppTitleBar.Visibility = Visibility.Collapsed;
                this.UseImmersiveDarkModeEx(userSettings.Settings.ApplicationTheme == AppTheme.Dark);
            }

            //using Gdi32.SafeHRGN rgn = InitTransparent();           
        }

        //public void Changedtransparent(bool isTransparent)
        //{
        //    if (isTransparent) NavView.Opacity = 0.5;
        //    else NavView.Opacity = 1;
        //    TransparentHelper.SetTransparent(this, isTransparent);
        //}

        // ref: https://learn.microsoft.com/zh-cn/windows/apps/design/controls/navigationview#backwards-navigation
        private void NavView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            // Add handler for ContentFrame navigation.
            //ContentFrame.Navigated += On_Navigated;

            // NavView doesn't load any page by default, so load home page.
            NavView.SelectedItem = NavView.MenuItems[0];
        }

        private void NavigationView_SelectionChanged(
            NavigationView sender,
            NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected == false && args.SelectedItemContainer != null)
            {
                string tag = args.SelectedItemContainer.Tag.ToString();
                Page page = InstanceUtil<Page>.TryGetInstanceByName(tag, "");
                ContentFrame.Content = page;
            }
        }

        private void WindowEx_Closed(object sender, WindowEventArgs args)
        {
            if (_userSettingsClient.Settings.IsFirstRun)
            {
                args.Handled = true;
                _userSettingsClient.Settings.IsFirstRun = false;
                _userSettingsClient.Save<ISettings>();
                this.Close();
            }

            if (_userSettingsClient.Settings.IsUpdated)
            {
                args.Handled = true;
                _userSettingsClient.Settings.IsUpdated = false;
                _userSettingsClient.Save<ISettings>();
                this.Close();
            }
        }

        private void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                SetDragRegionForCustomTitleBar(this.AppWindow);
            }
        }

        private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (AppWindowTitleBar.IsCustomizationSupported()
                && this.AppWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                // Update drag region if the size of the title bar changes.
                SetDragRegionForCustomTitleBar(this.AppWindow);
            }
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                TitleTextBlock.Foreground =
                    (SolidColorBrush)App.Current.Resources["WindowCaptionForegroundDisabled"];
            }
            else
            {
                TitleTextBlock.Foreground =
                    (SolidColorBrush)App.Current.Resources["WindowCaptionForeground"];
            }
        }

        private void SetDragRegionForCustomTitleBar(AppWindow appWindow)
        {
            if (AppWindowTitleBar.IsCustomizationSupported()
                && appWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                double scaleAdjustment = GetScaleAdjustment();

                RightPaddingColumn.Width = new GridLength(appWindow.TitleBar.RightInset / scaleAdjustment);
                LeftPaddingColumn.Width = new GridLength(appWindow.TitleBar.LeftInset / scaleAdjustment);

                List<Windows.Graphics.RectInt32> dragRectsList = [];

                Windows.Graphics.RectInt32 dragRectL;
                dragRectL.X = (int)((LeftPaddingColumn.ActualWidth) * scaleAdjustment);
                dragRectL.Y = 0;
                dragRectL.Height = (int)(AppTitleBar.ActualHeight * scaleAdjustment);
                dragRectL.Width = (int)((IconColumn.ActualWidth
                                        + TitleColumn.ActualWidth
                                        + LeftDragColumn.ActualWidth) * scaleAdjustment);
                dragRectsList.Add(dragRectL);

                Windows.Graphics.RectInt32 dragRectR;
                dragRectR.X = (int)((LeftPaddingColumn.ActualWidth
                                    + IconColumn.ActualWidth
                                    + TitleTextBlock.ActualWidth
                                    + LeftDragColumn.ActualWidth) * scaleAdjustment);
                dragRectR.Y = 0;
                dragRectR.Height = (int)(AppTitleBar.ActualHeight * scaleAdjustment);
                dragRectR.Width = (int)(RightDragColumn.ActualWidth * scaleAdjustment);
                dragRectsList.Add(dragRectR);

                Windows.Graphics.RectInt32[] dragRects = dragRectsList.ToArray();

                appWindow.TitleBar.SetDragRectangles(dragRects);
            }
        }

        private double GetScaleAdjustment()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            DisplayArea displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
            IntPtr hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);

            // Get DPI.
            int result = GetDpiForMonitor(hMonitor, Monitor_DPI_Type.MDT_Default, out uint dpiX, out uint _);
            if (result != 0)
            {
                throw new Exception("Could not get DPI for monitor.");
            }

            uint scaleFactorPercent = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96);
            return scaleFactorPercent / 100.0;
        }

        [DllImport("Shcore.dll", SetLastError = true)]
        internal static extern int GetDpiForMonitor(IntPtr hmonitor, Monitor_DPI_Type dpiType, out uint dpiX, out uint dpiY);

        internal enum Monitor_DPI_Type : int
        {
            MDT_Effective_DPI = 0,
            MDT_Angular_DPI = 1,
            MDT_Raw_DPI = 2,
            MDT_Default = MDT_Effective_DPI
        }

        //private Gdi32.SafeHRGN InitTransparent()
        //{
        //    var windowHandle = new IntPtr((long)this.AppWindow.Id.Value);
        //    var rgn = Gdi32.CreateRectRgn(-2, -2, -1, -1);
        //    DwmApi.DwmEnableBlurBehindWindow(windowHandle, new DwmApi.DWM_BLURBEHIND()
        //    {
        //        dwFlags = DwmApi.DWM_BLURBEHIND_Mask.DWM_BB_ENABLE | DwmApi.DWM_BLURBEHIND_Mask.DWM_BB_BLURREGION,
        //        fEnable = true,
        //        hRgnBlur = rgn,
        //    });

        //    wndProcHandler = new ComCtl32.SUBCLASSPROC(WndProc);
        //    ComCtl32.SetWindowSubclass(windowHandle, wndProcHandler, 1, IntPtr.Zero);
        //    return rgn;
        //}

        //private unsafe IntPtr WndProc(HWND hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, IntPtr dwRefData)
        //{
        //    if (uMsg == (uint)User32.WindowMessage.WM_PAINT)
        //    {
        //        var hdc = User32.BeginPaint(hWnd, out var ps);
        //        if (hdc.IsNull) return new IntPtr(0);

        //        var brush = Gdi32.GetStockObject(Gdi32.StockObjectType.BLACK_BRUSH);
        //        User32.FillRect(hdc, ps.rcPaint, brush);
        //        return new IntPtr(1);
        //    }

        //    return ComCtl32.DefSubclassProc(hWnd, uMsg, wParam, lParam);
        //}

        //ComCtl32.SUBCLASSPROC wndProcHandler;

        private IUserSettingsClient _userSettingsClient;
        private MainWindowViewModel _viewModel;
    }
}
