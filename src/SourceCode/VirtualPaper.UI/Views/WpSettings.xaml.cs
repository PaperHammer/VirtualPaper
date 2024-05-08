using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using VirtualPaper.UI.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WpSettings : Page
    {
        public WpSettings()
        {
            this.InitializeComponent();

            _viewModel = App.Services.GetRequiredService<WpSettingsViewModel>();
            this.DataContext = _viewModel;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel.InitUpdateLayout();

            // NavView doesn't load any page by default, so load home page.
            _viewModel.InitNavItems();
        }

        private async void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            BtnClose.IsEnabled = false;

            _viewModel.Close();

            await Task.Delay(3000);
            BtnClose.IsEnabled = true;
        }

        private async void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            BtnRestore.IsEnabled = false;

            await _viewModel.RestoreAsync();

            await Task.Delay(3000);
            BtnRestore.IsEnabled = true;
        }

        private async void BtnDetect_Click(object sender, RoutedEventArgs e)
        {
            BtnDetect.IsEnabled = false;

            await _viewModel.DetectAsync(this.XamlRoot);

            await Task.Delay(3000);
            BtnDetect.IsEnabled = true;
        }

        private async void BtnIdentify_Click(object sender, RoutedEventArgs e)
        {
            BtnIdentify.IsEnabled = false;

            await _viewModel.IdentifyAsync();

            await Task.Delay(3000);
            BtnIdentify.IsEnabled = true;
        }

        private async void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.PreviewAsync();
        }

        private async void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            BtnApply.IsEnabled = false;

            await _viewModel.ApplyAsync(this.XamlRoot);

            await Task.Delay(3000);
            BtnApply.IsEnabled = true;
        }

        private void NavigationView_SelectionChanged(
            NavigationView sender,
            NavigationViewSelectionChangedEventArgs args)
        {
            string tag = args.SelectedItemContainer.Tag.ToString();
            _viewModel.TryNavPage(tag);
        }

        private WpSettingsViewModel _viewModel;
    }
}
