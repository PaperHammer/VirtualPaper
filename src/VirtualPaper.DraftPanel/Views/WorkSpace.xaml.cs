using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.Draft;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.UIComponent.Attributes;
using VirtualPaper.UIComponent.Navigation;
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

            _viewModel.TabViewItems.CollectionChanged += TabViewItems_CollectionChanged;
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

        private void TabViewItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null) {
                foreach (ArcTabViewItem newItem in e.NewItems) {
                    if (newItem.Tag is IRuntime runtime) {
                        var frame = new Frame {
                            Content = runtime,
                            Visibility = Visibility.Collapsed
                        };
                        _tabToFrame[newItem] = frame;
                        workspaceContentPool.Children.Add(frame);
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null) {
                foreach (ArcTabViewItem oldItem in e.OldItems) {
                    if (oldItem.Tag is IRuntime runtime &&
                        _tabToFrame.TryGetValue(oldItem, out var frame)) {
                        workspaceContentPool.Children.Remove(frame);
                        _tabToFrame.Remove(oldItem);
                        frame.Content = null;
                    }
                }
            }
        }


        #region create new
        public void ShowOverlayPage(Type pageType, object? parameter) {
            maskGrid.Visibility = Visibility.Visible;
            overlayFrame.Navigate(pageType, parameter);
        }

        public async void HideOverlayPage() {
            overlayFrame.Content = null;
            overlayFrame.BackStack.Clear();
            overlayFrame.ForwardStack.Clear();
            maskGrid.Visibility = Visibility.Collapsed;
        }

        private void MaskGrid_Tapped(object sender, TappedRoutedEventArgs e) {
            HideOverlayPage();
        }

        private void OverlayFrame_Tapped(object sender, TappedRoutedEventArgs e) {
            e.Handled = true;
        }

        private async void TabViewControl_AddTabButtonClick(TabView sender, object args) {
            Payload?.Set(NaviPayloadKey.TargetDraftPanelState, DraftPanelState.DraftConfig);
            Payload?.Set(NaviPayloadKey.IsFromWorkSpace, true);

            var tcs = new TaskCompletionSource<PreProjectData[]?>();
            Payload?.Set(NaviPayloadKey.DraftConfigTCS, tcs);
            ShowOverlayPage(typeof(ConfigSpace), Payload);

            try {
                var result = await tcs.Task;
                await _viewModel.AddNewItemsAsync(result);
                HideOverlayPage();
            }
            catch (Exception ex) {
                HideOverlayPage();
                ArcLog.GetLogger<WorkSpace>().Error(ex);
            }
        }
        #endregion

        private void TabViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.RemovedItems.FirstOrDefault() is ArcTabViewItem removedItem &&
                removedItem.Tag is IRuntime removedRuntime &&
                _tabToFrame.TryGetValue(removedItem, out var removedFrame)) {
                removedFrame.Visibility = Visibility.Collapsed;
            }

            if (e.AddedItems.FirstOrDefault() is ArcTabViewItem addedItem &&
                addedItem.Tag is IRuntime addedRuntime &&
                _tabToFrame.TryGetValue(addedItem, out var addedFrame)) {
                addedFrame.Visibility = Visibility.Visible;
            }
        }

        private async void TabViewControl_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args) {
            if (args.Tab is not ArcTabViewItem tabViewItem || tabViewItem.Tag is not IRuntime runtime) return;
            var res = await _viewModel.CheckSaveStatusAsync(runtime);
            if (res && _tabToFrame.TryGetValue(tabViewItem, out var frame)) {
                workspaceContentPool.Children.Remove(tabViewItem);
                _tabToFrame.Remove(tabViewItem);
                frame.Content = null;
            }
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
        private readonly Dictionary<ArcTabViewItem, Frame> _tabToFrame = [];
    }
}
