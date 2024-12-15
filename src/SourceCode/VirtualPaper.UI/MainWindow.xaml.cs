using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UI.Utils;
using VirtualPaper.UI.ViewModels;
using VirtualPaper.UIComponent.Utils.Extensions;
using WinRT.Interop;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WindowEx {
        //public List<WindowEx> ChildWindows { get; } = [];

        public string WindowStyleType { get; private set; }
        public SolidColorBrush WindowCaptionForeground { get; private set; }
        public SolidColorBrush WindowCaptionForegroundDisabled { get; private set; }

        public MainWindow(
            MainWindowViewModel mainWindowViewModel,
            IWallpaperControlClient wallpaperControlClient,
            IUserSettingsClient userSettingsClient) {
            _wpControl = wallpaperControlClient;
            _userSettings = userSettingsClient;

            this.InitializeComponent();

            _viewModel = mainWindowViewModel;
            this.NavView.DataContext = _viewModel;

            WindowCaptionForeground = (SolidColorBrush)App.Current.Resources["WindowCaptionForeground"];
            WindowCaptionForegroundDisabled = (SolidColorBrush)App.Current.Resources["WindowCaptionForegroundDisabled"];

            SetWindowStyle();
            SetWindowTitleBar();

            //this.Activate();
            //using Gdi32.SafeHRGN rgn = InitTransparent();           
        }

        private void SetWindowTitleBar() {
            //ref: https://learn.microsoft.com/en-us/windows/apps/develop/title-bar?tabs=wasdk
            if (AppWindowTitleBar.IsCustomizationSupported()) {
                var titleBar = this.AppWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonForegroundColor = WindowCaptionForeground.Color;

                AppTitleBar.Loaded += AppTitleBar_Loaded;
                AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
                //this.Activated += WindowEx_Activated;
            }
            else {
                AppTitleBar.Visibility = Visibility.Collapsed;
                this.UseImmersiveDarkModeEx(_userSettings.Settings.ApplicationTheme == AppTheme.Dark);
            }
        }

        private void SetWindowStyle() {
            string type = _userSettings.Settings.SystemBackdrop.ToString();
            WindowStyleType = type;
            this.SystemBackdrop = type switch {
                "Mica" => new MicaBackdrop(),
                "Acrylic" => new DesktopAcrylicBackdrop(),
                _ => default,
            };
        }

        private void WindowEx_Activated(object sender, WindowActivatedEventArgs args) {
            if (args.WindowActivationState == WindowActivationState.Deactivated) {
                TitleTextBlock.Foreground = WindowCaptionForegroundDisabled;
            }
            else {
                TitleTextBlock.Foreground = WindowCaptionForeground;
            }
        }

        private async void WindowEx_Closed(object sender, WindowEventArgs args) {
            await _wpControl.CloseAllPreviewAsync();

            if (_userSettings.Settings.IsFirstRun) {
                args.Handled = true;
                _userSettings.Settings.IsFirstRun = false;
                _userSettings.Save<ISettings>();
                this.Close();
            }

            if (_userSettings.Settings.IsUpdated) {
                args.Handled = true;
                _userSettings.Settings.IsUpdated = false;
                _userSettings.Save<ISettings>();
                this.Close();
            }

            //foreach (var window in ChildWindows) {
            //    window?.Close();
            //}

            App.ShutDown();
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
            RoutedEventArgs e) {
            // Add handler for ContentFrame navigation.
            ContentFrame.Navigated += On_Navigated;

            // NavView doesn't load any page by default, so load home page.
            NavView.SelectedItem = NavView.MenuItems[0];
        }

        private void NavigationView_SelectionChanged(
            NavigationView sender,
            NavigationViewSelectionChangedEventArgs args) {
            if (args.SelectedItemContainer != null) {
                Type navPageType = Type.GetType(args.SelectedItemContainer.Tag.ToString());
                NavView_Navigate(navPageType, args.RecommendedNavigationTransitionInfo);
            }
        }

        private void NavView_Navigate(
            Type navPageType,
            NavigationTransitionInfo transitionInfo) {
            // Get the page type before navigation so you can prevent duplicate
            // entries in the backstack.
            Type preNavPageType = ContentFrame.CurrentSourcePageType;

            // Only navigate if the selected page isn't currently loaded.
            if (navPageType is not null && !Type.Equals(preNavPageType, navPageType)) {
                BasicUIComponentUtil.Loading(false, false, []);
                ContentFrame.Navigate(navPageType, null, transitionInfo);
            }
        }

        private void On_Navigated(object sender, NavigationEventArgs e) {
            if (ContentFrame.SourcePageType != null) {
                // Select the nav view item that corresponds to the page being navigated to.
                var item =
                    NavView.MenuItems
                    .OfType<NavigationViewItem>()
                    .FirstOrDefault(i => i.Tag.Equals(ContentFrame.SourcePageType.FullName.ToString()), null)
                    ?? NavView.FooterMenuItems
                        .OfType<NavigationViewItem>()
                        .First(i => i.Tag.Equals(ContentFrame.SourcePageType.FullName.ToString()));

                NavView.SelectedItem = item;
            }
            BasicUIComponentUtil.Loaded([]);
        }

        #region window title bar
        private void AppTitleBar_Loaded(object sender, RoutedEventArgs e) {
            if (AppWindowTitleBar.IsCustomizationSupported()) {
                SetDragRegionForCustomTitleBar(this.AppWindow);
            }
        }

        private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (AppWindowTitleBar.IsCustomizationSupported()
                && this.AppWindow.TitleBar.ExtendsContentIntoTitleBar) {
                // Update drag region if the size of the title bar changes.
                SetDragRegionForCustomTitleBar(this.AppWindow);
            }
        }

        private void SetDragRegionForCustomTitleBar(AppWindow appWindow) {
            if (AppWindowTitleBar.IsCustomizationSupported()
                && appWindow.TitleBar.ExtendsContentIntoTitleBar) {
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

        private double GetScaleAdjustment() {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            DisplayArea displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
            IntPtr hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);

            // Get DPI.
            int result = Native.GetDpiForMonitor(hMonitor, Native.Monitor_DPI_Type.MDT_Default, out uint dpiX, out uint _);
            if (result != 0) {
                throw new Exception("Could not get DPI for monitor.");
            }

            uint scaleFactorPercent = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96);
            return scaleFactorPercent / 100.0;
        }
        #endregion

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

        private readonly IUserSettingsClient _userSettings;
        private readonly IWallpaperControlClient _wpControl;
        private readonly MainWindowViewModel _viewModel;
    }
}
