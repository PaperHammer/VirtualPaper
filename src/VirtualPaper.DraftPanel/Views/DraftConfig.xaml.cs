using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.DraftPanel.ViewModels;

// To learn more about WinUI, the WinUI draft structure,
// and more about our draft templates, see: http://aka.ms/winui-draft-info.

namespace VirtualPaper.DraftPanel.Views {
    public sealed partial class DraftConfig : Page {
        public DraftConfig() {
            this.InitializeComponent();

            _viewModel = new();
            this.DataContext = _viewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (this._draftPanel == null) {
                this._draftPanel = e.Parameter as IDraftPanelBridge;
            }
        }

        private void ChangeFolderButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            _viewModel.ChangeFolder(_draftPanel.GetWindowHandle());
        }

        private void PreviousStepButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            _draftPanel.ChangeProjectPanelState(DraftPanelState.ProjectConfig);
        }

        private async void CreateVpdButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            bool storagePathIsAvailable = _viewModel.CreateNewDir();
            if (!storagePathIsAvailable) {

                return;
            }


        }

        private readonly DraftConfigViewModel _viewModel;
        private IDraftPanelBridge _draftPanel;
    }
}
