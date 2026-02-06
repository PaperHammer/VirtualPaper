using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.UIComponent.Attributes;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils.Extensions;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    [KeepAlive]
    public sealed partial class WorkSpace : ArcPage {
        public override ArcPageContext Context { get; set; }
        public override Type PageType => typeof(WorkSpace);

        public WorkSpace() {
            this.Unloaded += WorkSpace_Unloaded;
            this.InitializeComponent();
            _viewModel = ObjectProvider.GetRequiredService<WorkSpaceViewModel>(ObjectLifetime.Transient, ObjectLifetime.Singleton);
            this.DataContext = _viewModel;
            Context = new ArcPageContext(this);
        }

        private void WorkSpace_Unloaded(object sender, RoutedEventArgs e) {
            this.Unloaded -= WorkSpace_Unloaded;
            this._viewModel?.Dispose();
            this._viewModel = null;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (e.Parameter is NavigationPayload payload) {
                payload.TryGet(NaviPayloadKey.Project.ToString(), out _project);
            }
        }

        private void TabViewControl_Loaded(object sender, RoutedEventArgs e) {
            if (_project == null) return;
            _viewModel.InitTabViewItems(_project);
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

        private WorkSpaceViewModel _viewModel;
        private ProjectData? _project;
    }
}
