using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.Bridge.Base;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.WpSettingsPanel.ViewModels;
using VirtualPaper.WpSettingsPanel.Views;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.WpSettingsPanel {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WpSettings : Page, IWpSettingsPanel {
        internal static IWpSettingsPanel Instance { get; private set; }

        public WpSettings() {
            Instance = this;
            this.InitializeComponent();
        }

        #region bridge
        public nint GetWindowHandle() {
            return _windowBridge.GetWindowHandle();
        }

        public INoifyBridge GetNotify() {
            return _windowBridge.GetNotify();
        }

        public void Log(LogType type, object message) {
            _windowBridge.Log(type, message);
        }

        public object GetMainWindow() {
            return _windowBridge.GetMainWindow();
        }

        public IDialogService GetDialog() {
            return _windowBridge.GetDialog();
        }
        #endregion

        #region nav
        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (this._windowBridge == null) {
                ContentFrame.CacheSize = 2;
                this._windowBridge = e.Parameter as IWindowBridge;
                _viewModel = ObjectProvider.GetRequiredService<WpSettingsViewModel>(ObjectLifetime.Singleton, ObjectLifetime.Singleton);
                _viewModel._wpSettingsPanel = this;
                this.DataContext = _viewModel;
            }
        }

        private void NvLocal_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args) {
            try {
                FrameNavigationOptions navOptions = new() {
                    TransitionInfoOverride = args.RecommendedNavigationTransitionInfo,
                    IsNavigationStackEnabled = false
                };

                Type pageType = null;
                if (args.SelectedItemContainer.Name == Nav_LibraryContents.Name) {
                    pageType = typeof(LibraryContents);
                }
                else if (args.SelectedItemContainer.Name == Nav_ScreenSaver.Name) {
                    pageType = typeof(ScreenSaver);
                }

                ContentFrame.NavigateToType(pageType, this, navOptions);
            }
            catch (Exception ex) {
                _windowBridge.GetNotify().ShowExp(ex);
                _windowBridge.Log(LogType.Error, ex);
            }
        }
        #endregion     

        private void Flyout_Opening(object sender, object e) {
            _viewModel.InitWpArrangments();
            _viewModel.InitMonitors(); // 打开该页面不会触发绑定值修改，需要手动调用更新
        }

        #region btn_click
        private async void BtnClose_Click(object sender, RoutedEventArgs e) {
            BtnClose.IsEnabled = false;
            _viewModel.Close();
            await Task.Delay(3000);
            BtnClose.IsEnabled = true;
        }

        private async void BtnDetect_Click(object sender, RoutedEventArgs e) {
            BtnDetect.IsEnabled = false;
            _viewModel.Detect();
            await Task.Delay(3000);
            BtnDetect.IsEnabled = true;
        }

        private async void BtnIdentify_Click(object sender, RoutedEventArgs e) {
            BtnIdentify.IsEnabled = false;
            await _viewModel.IdentifyAsync();
            await Task.Delay(3000);
            BtnIdentify.IsEnabled = true;
        }

        private async void BtnAdjust_Click(object sender, RoutedEventArgs e) {
            await _viewModel.AdjustAsync();
        }
        #endregion

        private IWindowBridge _windowBridge;
        private WpSettingsViewModel _viewModel;
    }
}
