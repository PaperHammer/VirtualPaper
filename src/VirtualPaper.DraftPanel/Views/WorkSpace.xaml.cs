using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Runtime.Draft;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.DraftPanel.Views.ConfigSpaceComponents;
using VirtualPaper.UIComponent.Attributes;
using VirtualPaper.UIComponent.Navigation.Interfaces;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    [KeepAlive]
    public sealed partial class WorkSpace : ArcPage, IConfirmClose {
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
                Payload = Payload.Merge(payload);
            }
        }

        public async Task<bool> CanCloseAsync() {
            return await _viewModel.CheckAllSaveStatusAsync();
        }

        private async void TabViewControl_Loaded(object sender, RoutedEventArgs e) {
            if (_preProjectDatas == null) return;
            await _viewModel.AddNewItemsAsync(_preProjectDatas);
        }

        private void TabViewControl_TabItemsChanged(TabView sender, Windows.Foundation.Collections.IVectorChangedEventArgs args) {
            _viewModel.OnTabItemsChanged(sender, args);
        }

        #region create new
        public void ShowOverlayPage(Type pageType, object? parameter) {
            maskGrid.Visibility = Visibility.Visible;
            overlayFrame.Navigate(pageType, parameter);
        }

        public void HideOverlayPage() {
            maskGrid.Visibility = Visibility.Collapsed;
            overlayFrame.Content = null;
            overlayFrame.BackStack.Clear();
            overlayFrame.ForwardStack.Clear();
        }

        private void MaskGrid_Tapped(object sender, TappedRoutedEventArgs e) {
            HideOverlayPage();
        }

        private void OverlayFrame_Tapped(object sender, TappedRoutedEventArgs e) {
            e.Handled = true;
        }

        private void TabViewControl_AddTabButtonClick(TabView sender, object args) {
            //_viewModel.AddDraftItem();
            Payload?.Set(NaviPayloadKey.TargetDraftPanelState, DraftPanelState.DraftConfig);
            Payload?.Set(NaviPayloadKey.IsFromWorkSpace, true);
            Payload?.Set(NaviPayloadKey.DraftConfigPreBtnAction, new Action(HideOverlayPage));
            Payload?.Set(NaviPayloadKey.DraftConfigNxtBtnAction, new Action<object?>(async (targetData) => {
                await _viewModel.AddNewItemsAsync(targetData as PreProjectData[]);
                HideOverlayPage();
            }));
            ShowOverlayPage(typeof(ConfigSpace), Payload);
        }
        #endregion

        private async void TabViewControl_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args) {
            if (args.Tab.Content is not IRuntime runtime) return;
            await _viewModel.CheckSaveStatusAsync(runtime);
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
