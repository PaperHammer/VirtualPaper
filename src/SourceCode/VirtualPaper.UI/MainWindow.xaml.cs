using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UI.Utils;
using VirtualPaper.UI.ViewModels;
using WinUI3Localizer;
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
        private MainWindowViewModel _viewModel;

        public MainWindow(
            IUserSettingsClient userSettings)
        {
            this.InitializeComponent();
            //using Gdi32.SafeHRGN rgn = InitTransparent();

            _userSettingsClient = userSettings;
            _localizer = Localizer.Get();

            _viewModel = new MainWindowViewModel();
            this.NavView.DataContext = _viewModel;
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
        private ILocalizer _localizer;
    }
}
