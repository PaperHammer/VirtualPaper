using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.DraftPanel.Model.Interfaces;
using VirtualPaper.DraftPanel.Model.NavParam;
using VirtualPaper.DraftPanel.ViewModels;

// To learn more about WinUI, the WinUI draft structure,
// and more about our draft templates, see: http://aka.ms/winui-draft-info.

namespace VirtualPaper.DraftPanel.Views.ConfigSpaceComponents {
    public sealed partial class DraftConfig : Page {
        public DraftConfig() {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            _viewModel = ObjectProvider.GetRequiredService<DraftConfigViewModel>(ObjectLifetime.Singleton, ObjectLifetime.Singleton);
            _viewModel._configSpace = e.Parameter as IConfigSpace;           
            _viewModel.InitContent();
            _viewModel.InitConfigSpace();
            this.DataContext = _viewModel;
        }

        private void ChangeFolderButton_Click(object sender, RoutedEventArgs e) {
            _viewModel.ChangeFolder();
        }

        private DraftConfigViewModel _viewModel;
    }
}
