using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.UI.ViewModels.AppSettings;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI.Views.AppSettingsConponents {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PerformanceSetting : Page {
        public PerformanceSetting() {
            this.InitializeComponent();

            _viewModel = App.Services.GetRequiredService<PerformanceSettingViewModel>();
            this.DataContext = _viewModel;
        }

        private readonly PerformanceSettingViewModel _viewModel;
    }
}
