using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.UIComponent.Attributes;
using VirtualPaper.UIComponent.Navigation;
using VirtualPaper.UIComponent.Navigation.Interfaces;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.StaticImg.Views.Components;
using Workloads.Utils.DraftUtils.Interfaces;
using Workloads.Utils.DraftUtils.Models;

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

        private void Page_Unloaded(object sender, RoutedEventArgs e) {
            _viewModel.TabViewItems.CollectionChanged -= TabViewItems_CollectionChanged;
            _preProjectDatas = null;
            _tabToFrame.Clear();
            _viewModel.Dispose();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            _viewModel.TabViewItems.CollectionChanged += TabViewItems_CollectionChanged;
            if (e.Parameter is FrameworkPayload payload) {
                payload.TryGet(NaviPayloadKey.DraftPage, out _draftPage);
                payload.TryGet(NaviPayloadKey.Project.ToString(), out _preProjectDatas);
                Payload = Payload.Merge(payload);
            }
        }

        public async Task<bool> CanCloseAsync() {
            return await _viewModel.CheckAllSaveStatusAsync();
        }

        #region add and selection
        private async void TabViewControl_Loaded(object sender, RoutedEventArgs e) {
            if (_preProjectDatas == null) return;
            await _viewModel.AddNewItemsAsync(_preProjectDatas);
        }

        private void TabViewItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
            SyncWorkspaceUI();
        }

        private void TabViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            SyncWorkspaceUI();
        }

        private void TabViewControl_TabItemsChanged(TabView sender, Windows.Foundation.Collections.IVectorChangedEventArgs args) {
            _viewModel.OnTabItemsChanged(sender, args);
        }

        private void SyncWorkspaceUI() {
            // 找出集合中有，但 UI 字典里没有的，创建 Frame
            foreach (var item in _viewModel.TabViewItems) {
                if (!_tabToFrame.ContainsKey(item) && item.Tag is IRuntime runtime) {
                    var frame = new Frame {
                        Content = runtime,
                        Visibility = Visibility.Collapsed
                    };
                    _tabToFrame[item] = frame;
                    workspaceContentPool.Children.Add(frame);
                }
            }

            // 找出 UI 字典里有，但集合里已经不存在的，彻底销毁 Frame
            var itemsToRemove = _tabToFrame.Keys.Where(k => !_viewModel.TabViewItems.Contains(k)).ToList();
            foreach (var item in itemsToRemove) {
                if (_tabToFrame.TryGetValue(item, out var frame)) {
                    workspaceContentPool.Children.Remove(frame);
                    _tabToFrame.Remove(item);
                    frame.Content = null;
                }
            }

            // 根据当前的选中项，控制可见性
            var selectedItem = TabViewControl.SelectedItem as ArcTabViewItem;
            foreach (var kvp in _tabToFrame) {
                kvp.Value.Visibility = (kvp.Key == selectedItem) ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        #endregion

        #region overlay page
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
        #endregion

        #region create new
        private async void TabViewControl_AddTabButtonClick(TabView sender, object args) {
            await GoToCreateNewAsync();
        }

        private async Task GoToCreateNewAsync() {
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

        private async void MFI_CreateNew_Clicked(object sender, RoutedEventArgs e) {
            await GoToCreateNewAsync();
        }
        #endregion

        #region close
        private async void TabViewControl_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args) {
            if (args.Tab is not ArcTabViewItem tabViewItem || tabViewItem.Tag is not IRuntime runtime) return;
            await TryCloseItemAsync(tabViewItem, runtime);
            if (_viewModel.TabViewItems.Count == 0) {
                _draftPage?.NavigateByState(DraftPanelState.ConfigSpace);
            }
        }

        private async Task TryCloseItemAsync(ArcTabViewItem tabViewItem, IRuntime runtime) {
            var res = await _viewModel.CheckSaveStatusAsync(runtime);
            if (res) {
                CleanUpTabUI(tabViewItem);
            }
        }

        private async void MFI_Exit_Clicked(object sender, RoutedEventArgs e) {
            await foreach (var tabViewItem in _viewModel.HandleExitItemsAsync()) {
                CleanUpTabUI(tabViewItem);
            }
            _draftPage?.NavigateByState(DraftPanelState.ConfigSpace);
        }

        private void CleanUpTabUI(ArcTabViewItem tabViewItem) {
            if (_tabToFrame.TryGetValue(tabViewItem, out var frame)) {
                workspaceContentPool.Children.Remove(tabViewItem);
                _tabToFrame.Remove(tabViewItem);
                frame.Content = null;
            }
        }
        #endregion

        #region export        
        private async void MFI_Export_Cliked(object sender, RoutedEventArgs e) {
            await GoToExportAsync();
        }

        private async Task GoToExportAsync() {
            var activeRuntime = _viewModel.GetSelectedItem();
            if (activeRuntime == null) return;

            var exportPageType = activeRuntime.ExportOverlayPageType;

            Payload?.Set(NaviPayloadKey.TargetDraftPanelState, DraftPanelState.ExportConfig);
            Payload?.Set(NaviPayloadKey.IsFromWorkSpace, true);

            var tcs = new TaskCompletionSource<IExportData>();
            Payload?.Set(NaviPayloadKey.DraftConfigTCS, tcs);
            ShowOverlayPage(exportPageType, Payload);

            var result = await tcs.Task;

            var ctx = ArcPageContextManager.GetContext<Draft>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            var ctsExport = new CancellationTokenSource();
            int finishedCnt = 0;
            int total = result.Count;
            await loadingCtx.RunWithProgressAsync(
                operation: async (token, reportProgress) => {
                    try {
                        await foreach (var exportedFilePath in _viewModel.ExportAsync(result, token)) {
                            reportProgress(++finishedCnt, total);
                        }
                    }
                    catch (Exception ex) when (
                        ex is OperationCanceledException ||
                        (ex is RpcException rpc && rpc.StatusCode == StatusCode.Cancelled)) {
                        GlobalMessageUtil.ShowCanceled();
                        return;
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<WorkSpace>().Error(ex);
                        GlobalMessageUtil.ShowException(ex);
                    }
                }, total: result.Count, cts: ctsExport);

            HideOverlayPage();
        }
        #endregion

        private Draft? _draftPage;
        private readonly WorkSpaceViewModel _viewModel;
        private PreProjectData[]? _preProjectDatas;
        private readonly Dictionary<ArcTabViewItem, Frame> _tabToFrame = [];
    }
}
