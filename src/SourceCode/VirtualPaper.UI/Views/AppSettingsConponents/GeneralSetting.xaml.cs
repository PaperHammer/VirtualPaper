using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using VirtualPaper.UI.ViewModels.AppSettings;
using Windows.System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI.Views.AppSettingsConponents
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GeneralSetting : Page
    {
        public GeneralSetting()
        {
            this.InitializeComponent();

            _viewModel = App.Services.GetRequiredService<GeneralSettingViewModel>();
            this.DataContext = _viewModel;
        }

        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            VersionCheckUpdate.IsEnabled = false;
            UpdateProgressRing.IsActive = true;

            await _viewModel.CheckUpdateAsync();

            VersionCheckUpdate.IsEnabled = true;
            UpdateProgressRing.IsActive = false;
        }

        private async void StartDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateProgressRing.IsActive = false;

            await _viewModel.StartDownloadAsync();
        }

        private async void HyperlinkButton_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("ms-settings:themes"));
        }

        private void ChangeFileStorageButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.WallpaperDirectoryChange(this.XamlRoot);
        }

        private void OpenFileStorageButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.OpenFolder();
        }

        private GeneralSettingViewModel _viewModel;
    }
}
