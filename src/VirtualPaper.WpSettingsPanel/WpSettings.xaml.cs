using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.UIComponent.Attributes;
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
    [KeepAlive]
    public sealed partial class WpSettings : ArcPage {
        public override ArcPageContext Context { get; }
        public override Type PageType => typeof(WpSettings);

        public WpSettings() {
            this.InitializeComponent();
            _viewModel = ObjectProvider.GetRequiredService<WpSettingsViewModel>(ObjectLifetime.Singleton, ObjectLifetime.Singleton);
            this.DataContext = _viewModel;
            Context = new ArcPageContext(this, this.MainHost.LoadingControlHost);
        }

        #region nav
        private void NvLocal_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args) {
            try {               
                Type pageType = args.SelectedItemContainer.Name switch {
                    "Nav_LibraryContents" => typeof(LibraryContents),
                    "Nav_ScreenSaver" => typeof(ScreenSaver),
                    _ => throw new NotImplementedException(),
                };

                ContentFrame.Navigate(pageType, this);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WpSettings>().Error(ex);
                GlobalMessageUtil.ShowException(ex, key: ex.Message);
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

        private readonly WpSettingsViewModel _viewModel;
    }
}
