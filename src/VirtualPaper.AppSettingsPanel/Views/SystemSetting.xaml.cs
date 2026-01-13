using Microsoft.UI.Xaml.Controls;
using VirtualPaper.AppSettingsPanel.ViewModels;
using VirtualPaper.Common.Utils.DI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.AppSettingsPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SystemSetting : Page {
        public SystemSetting() {                       
            _viewModel = ObjectProvider.GetRequiredService<SystemSettingViewModel>();
            this.DataContext = _viewModel;        
            this.InitializeComponent(); 
        }

        private readonly SystemSettingViewModel _viewModel;
    }
}
