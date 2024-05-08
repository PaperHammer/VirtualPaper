using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.UI.ViewModels;
using VirtualPaper.UI.ViewModels.WpSettingsComponents;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI.Views.WpSettingsComponents
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WpNavSettgins : Page
    {
        public WpNavSettgins()
        {
            this.InitializeComponent();
            _viewModel = App.Services.GetRequiredService<WpNavSettginsViewModel>();
            this.DataContext = _viewModel;
        }

        private async void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            var tag = ((RadioButton)sender).Tag.ToString();
            await _viewModel.UpdateWpArrange(tag);
        }

        private WpNavSettginsViewModel _viewModel;
    }
}
