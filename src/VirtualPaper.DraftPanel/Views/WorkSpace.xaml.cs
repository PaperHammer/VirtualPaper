using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.DraftPanel.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WorkSpace : Page {
        public WorkSpace() {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            _viewModel = ObjectProvider.GetRequiredService<WorkSpaceViewModel>(ObjectLifetime.Transient, ObjectLifetime.Singleton);
            this.DataContext = _viewModel;
        }

        private void TabViewControl_AddTabButtonClick(TabView sender, object args) {
            _viewModel.AddDraftItem();
        }

        private void TabViewControl_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args) {

        }

        private async void TabViewControl_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            await _viewModel.InitDraftItemAsync(Draft.DraftPanelBridge.GetParam() as string[]);
        }

        private void MFI_Exit_Clicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            _viewModel.Exit();
        }

        private void MFI_Save_Clicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            _viewModel.Save();
        }

        private void MFI_SaveAll_Clicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            _viewModel.SaveAll();
        }

        private WorkSpaceViewModel _viewModel;
    }
}
