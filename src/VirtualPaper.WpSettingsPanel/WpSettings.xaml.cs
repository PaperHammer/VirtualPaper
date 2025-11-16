using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Logging;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.WpSettingsPanel.ViewModels;
using VirtualPaper.WpSettingsPanel.Views;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.WpSettingsPanel {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WpSettings : ArcPage {
        public override ArcPageHost PageHost => this.MainHost;
        public override ArcPageContext Context { get; }
        public override Type PageType => typeof(WpSettings);

        public WpSettings() {
            Loaded += WpSettings_Loaded;
            this.InitializeComponent();
            Context = new ArcPageContext(this, this.MainHost.LoadingControlHost);
        }

        private void WpSettings_Loaded(object sender, RoutedEventArgs e) {
            _viewModel = ObjectProvider.GetRequiredService<WpSettingsViewModel>(ObjectLifetime.Singleton, ObjectLifetime.Singleton);
            this.DataContext = _viewModel;
        }

        #region nav
        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);
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
                ArcLog.GetLogger<WpSettings>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
            }
        }
        #endregion     

        private void Flyout_Opening(object sender, object e) {
            _viewModel.InitWpArrangments();
            _viewModel.InitMonitors(); // 打开该页面不会触发绑定值修改，需要手动调用更新
        }

        #region btn_click
        private void BtnClose_Click(object sender, RoutedEventArgs e) {
            BtnClose.IsEnabled = false;
            _viewModel.Close();
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

        private WpSettingsViewModel _viewModel;
    }
}
