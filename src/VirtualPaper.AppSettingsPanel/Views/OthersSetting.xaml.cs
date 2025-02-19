using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.AppSettingsPanel.ViewModels;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.DI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.AppSettingsPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class OthersSetting : Page {
        public OthersSetting() {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (this._appSettingsPanel == null) {
                this._appSettingsPanel = e.Parameter as IAppSettingsPanel;

                _viewModel = ObjectProvider.GetRequiredService<OtherSettingViewModel>(ObjectLifetime.Singleton, ObjectLifetime.Singleton);
                this.DataContext = _viewModel;
            }
        }

        private OtherSettingViewModel _viewModel;
        private IAppSettingsPanel _appSettingsPanel;
    }
}
