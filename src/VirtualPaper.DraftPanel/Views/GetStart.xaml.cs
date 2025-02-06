using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Models.ProjectPanel;
using VirtualPaper.DraftPanel.StrategyGroup.StartupSTG;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Common.Utils.Bridge;

// To learn more about WinUI, the WinUI draft structure,
// and more about our draft templates, see: http://aka.ms/winui-draft-info.

namespace VirtualPaper.DraftPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GetStart : Page {
        public GetStart() {
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

        private void OnFilterChanged(object sender, TextChangedEventArgs e) {
            var filtered = _viewModel._recentUsed.Where(Filter);
            Remove_NonMatching(filtered);
            AddBack_Procs(filtered);
        }

        private bool Filter(RecentUsed recentUsed) {
            return recentUsed.ProjectName.Contains(TargetName.Text, StringComparison.InvariantCultureIgnoreCase);
        }

        private void Remove_NonMatching(IEnumerable<RecentUsed> recentuseds) {
            for (int i = _viewModel.RecentUseds.Count - 1; i >= 0; i--) {
                var item = _viewModel.RecentUseds[i];
                if (!recentuseds.Contains(item)) {
                    _viewModel.RecentUseds.Remove(item);
                }
            }
        }

        private void AddBack_Procs(IEnumerable<RecentUsed> recentuseds) {
            foreach (var item in recentuseds) {
                if (!_viewModel.RecentUseds.Contains(item)) {
                    _viewModel.RecentUseds.Add(item);
                }
            }
        }

        private void ContinueWithoutAny_HyperlinkButton_Click(object sender, RoutedEventArgs e) {
            ToWorkSpace();
        }

        private void RecentUsedsListView_ItemClick(object sender, ItemClickEventArgs e) {
            ToWorkSpace(e.ClickedItem);
        }

        private async void StartupItemsView_ItemInvoked(ItemsView sender, ItemsViewItemInvokedEventArgs args) {
            var startUp = args.InvokedItem as Startup;
            foreach (var stg in _strategies) {
                if (stg.CanHandle(startUp.Type)) {
                    await stg.HandleAsync(_draftPanel);
                }
            }
        }

        private void ToWorkSpace(object proj = null) {
            _draftPanel.ChangeProjectPanelState(DraftPanelState.WorkSpace, proj);
        }

        private readonly GetStartViewModel _viewModel;
        private IDraftPanelBridge _draftPanel;
        private readonly IStrategy[] _strategies = [
            new OpenVpd(),
            new OpenFile(),
            new OpenFolder(),
            new NewVpd(),
        ];
    }
}
