using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.DraftPanel.Model.Interfaces;
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
            this.DataContext = _viewModel;

            _viewModel.InitContent();
            _viewModel._configSpace.SetPreviousStepBtnText(_viewModel.Project_DeployNewDraft_PreviousStep);
            _viewModel._configSpace.SetNextStepBtnText(_viewModel.Project_DeployNewDraft_Create);
            _viewModel._configSpace.SetBtnVisible(true);
            _viewModel._configSpace.BindingPreviousBtnAction(PreviousStepBtnAction);
            _viewModel._configSpace.BindingNextBtnAction((s, args) => { });
        }

        private void ChangeFolderButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            _viewModel.ChangeFolder(_viewModel._configSpace.GetWindowHandle());
        }

        private void PreviousStepBtnAction(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            _viewModel._configSpace.ChangePanelState(DraftPanelState.ProjectConfig);
        }

        //private async void CreateVpdBtnAction(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
        //    bool storagePathIsAvailable = _viewModel.CreateNewDir();
        //    if (!storagePathIsAvailable) {

        //        return;
        //    }


        //}

        private DraftConfigViewModel _viewModel;
    }
}
