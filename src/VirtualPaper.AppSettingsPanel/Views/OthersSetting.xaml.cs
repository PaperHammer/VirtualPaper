using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.AppSettingsPanel.ViewModels;
using VirtualPaper.Common.Utils.DI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.AppSettingsPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class OthersSetting : Page {
        public OthersSetting() {
            this.Unloaded += OthersSetting_Unloaded;
            this.InitializeComponent();                   
            _viewModel = AppServiceLocator.Services.GetRequiredService<OtherSettingViewModel>();
            this.DataContext = _viewModel;            
        }

        private void OthersSetting_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            this.DataContext = null;
            this.Unloaded -= OthersSetting_Unloaded;
        }

        private readonly OtherSettingViewModel _viewModel;
    }
}
