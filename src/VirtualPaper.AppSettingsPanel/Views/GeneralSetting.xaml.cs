using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.AppSettingsPanel.ViewModels;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.DI;
using Windows.System;
using WinRT;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.AppSettingsPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GeneralSetting : Page {
        public GeneralSetting() {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (this._appSettingsPanel == null) {
                this._appSettingsPanel = e.Parameter as IAppSettingsPanel;

                _viewModel = ObjectProvider.GetRequiredService<GeneralSettingViewModel>(ObjectLifetime.Singleton, ObjectLifetime.Singleton);
                _viewModel._appSettingsPanel = this._appSettingsPanel;
                this.DataContext = _viewModel;
            }
        }

        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e) {
            VersionCheckUpdate.IsEnabled = false;
            UpdateProgressRing.IsActive = true;

            await _viewModel.CheckUpdateAsync();

            VersionCheckUpdate.IsEnabled = true;
            UpdateProgressRing.IsActive = false;
        }

        private async void StartDownloadButton_Click(object sender, RoutedEventArgs e) {
            UpdateProgressRing.IsActive = false;

            await _viewModel.StartDownloadAsync();
        }

        private async void HyperlinkButton_Click(object _, RoutedEventArgs e) {
            await Launcher.LaunchUriAsync(new Uri("ms-settings:themes"));
        }

        private void ChangeFileStorageButton_Click(object sender, RoutedEventArgs e) {
            _viewModel.WallpaperDirectoryChange();
        }

        private void OpenFileStorageButton_Click(object sender, RoutedEventArgs e) {
            _viewModel.OpenFolder();
        }

        private GeneralSettingViewModel _viewModel;
        private IAppSettingsPanel _appSettingsPanel;
    }
}
