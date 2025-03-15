using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.DraftPanel.Model.Interfaces;
using VirtualPaper.DraftPanel.Model.StrategyGroup.StartupSTG;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Models.DraftPanel;

// To learn more about WinUI, the WinUI draft structure,
// and more about our draft templates, see: http://aka.ms/winui-draft-info.

namespace VirtualPaper.DraftPanel.Views.ConfigSpaceComponents {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GetStart : Page {
        public GetStart() {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            this._configSpace ??= e.Parameter as IConfigSpace;
            _viewModel = ObjectProvider.GetRequiredService<GetStartViewModel>(ObjectLifetime.Singleton, ObjectLifetime.Singleton);
            this.DataContext = _viewModel;

            this._configSpace.SetBtnVisible(false);
        }

        private void OnFilterChanged(object sender, TextChangedEventArgs e) {
            var filtered = _viewModel._recentUsed.Where(Filter);
            Remove_NonMatching(filtered);
            AddBack_Procs(filtered);
        }

        private bool Filter(RecentUsed recentUsed) {
            return recentUsed.ProjectName.Contains(tbSearchName.Text, StringComparison.InvariantCultureIgnoreCase);
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
            if (e.ClickedItem is RecentUsed ru) {
                ToWorkSpace(new List<string> { ru.FilePath });
            }
        }

        private async void StartupItemsView_ItemInvoked(ItemsView sender, ItemsViewItemInvokedEventArgs args) {
            await HandleStartupAsync(args.InvokedItem as Startup);
        }

        private async Task HandleStartupAsync(Startup startUp) {
            foreach (var stg in _strategies) {
                if (stg.CanHandle(startUp.Type)) {
                    await stg.HandleAsync(_configSpace);
                    break;
                }
            }
        }

        private void ToWorkSpace(object param = null) {
            _configSpace.ChangePanelState(DraftPanelState.WorkSpace, param);
        }

        private void KeyboardAccelerator_Invoked_RecentUseds(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) {
            FocusOnFirstItem();
            args.Handled = true;
        }

        private void FocusOnFirstItem() {
            if (lvRecentUsed.Items.Count > 0) {
                var firstItemContainer = lvRecentUsed.ContainerFromIndex(0) as ListViewItem;
                firstItemContainer?.Focus(FocusState.Programmatic);
            }
        }

        private void KeyboardAccelerator_Invoked_SearchRecentUsed(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) {
            tbSearchName.Focus(FocusState.Programmatic);
            args.Handled = true;
        }

        private async void KeyboardAccelerator_Invoked_Startups(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) {
            var startup = (ivStartups.ItemsSource as List<Startup>).Find(x => x.ShortCut == args.KeyboardAccelerator.Key);
            if (startup != null) {
                await HandleStartupAsync(startup);
            }
            args.Handled = true;
        }

        private GetStartViewModel _viewModel;
        private IConfigSpace _configSpace;
        internal readonly IStrategy[] _strategies = [
            new OpenVpd(),
            new OpenFile(),
            new NewVpd(),
        ];
    }
}
