using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.UIComponent.Attributes;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    [KeepAlive]
    public sealed partial class WorkSpace : ArcPage {
        public override Type ArcType => typeof(WorkSpace);

        public WorkSpace() {
            this.InitializeComponent();
            _viewModel = AppServiceLocator.Services.GetRequiredService<WorkSpaceViewModel>();
            this.DataContext = _viewModel;            
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (e.Parameter is FrameworkPayload payload) {
                payload.TryGet(NaviPayloadKey.Project.ToString(), out _preProjectDatas);
            }
        }

        private async void TabViewControl_Loaded(object sender, RoutedEventArgs e) {
            if (_preProjectDatas == null) return;
            await _viewModel.InitTabViewItems(_preProjectDatas);
        }

        private void TabViewControl_TabItemsChanged(TabView sender, Windows.Foundation.Collections.IVectorChangedEventArgs args) {
            _viewModel.OnTabItemsChanged(sender, args);
        }

        private void TabViewControl_AddTabButtonClick(TabView sender, object args) {
            _viewModel.AddDraftItem();
        }

        private void TabViewControl_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args) {

        }

        private async void MFI_Exit_Clicked(object sender, RoutedEventArgs e) {
            await _viewModel.ExitAsync();
        }

        private async void MFI_Save_Clicked(object sender, RoutedEventArgs e) {
            await _viewModel.SaveAsync();
        }

        private async void MFI_SaveAll_Clicked(object sender, RoutedEventArgs e) {
            await _viewModel.SaveAllAsync();
        }

        private async void MFI_Undo_Clicked(object sender, RoutedEventArgs e) {
            await _viewModel.UndoAsync();
        }

        private async void MFI_Redo_Clicked(object sender, RoutedEventArgs e) {
            await _viewModel.RedoAsync();
        }

        private readonly WorkSpaceViewModel _viewModel;
        private PreProjectData[]? _preProjectDatas;
    }
}
