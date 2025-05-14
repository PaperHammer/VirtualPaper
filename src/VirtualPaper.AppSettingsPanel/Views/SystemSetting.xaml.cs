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
    public sealed partial class SystemSetting : Page {
        public SystemSetting() {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (this._appSettingsPanel == null) {
                this._appSettingsPanel = e.Parameter as IAppSettingsPanel;

                _viewModel = ObjectProvider.GetRequiredService<SystemSettingViewModel>(ObjectLifetime.Singleton, ObjectLifetime.Singleton);
                _viewModel._appSettingsPanel = this._appSettingsPanel;
                this.DataContext = _viewModel;
            }
        }

        private void DebugButton_Click(object sender, RoutedEventArgs e) {
            _viewModel.OpenDebugView();
        }

        private async void LogButton_Click(object sender, RoutedEventArgs e) {
            await _viewModel.ExportLogsAsync();
        }

        private IAppSettingsPanel _appSettingsPanel;
        private SystemSettingViewModel _viewModel;
    }
}
