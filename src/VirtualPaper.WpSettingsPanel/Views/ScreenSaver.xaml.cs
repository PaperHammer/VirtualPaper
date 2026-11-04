using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Models;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.WpSettingsPanel.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.WpSettingsPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ScreenSaver : ArcPage {
        public override Type ArcType => typeof(ScreenSaver);

        public ScreenSaver() {
            this.InitializeComponent();
            this.Unloaded += ScreenSaver_Unloaded;            
            _viewModel = AppServiceLocator.Services.GetRequiredService<ScreenSaverViewModel>();
            this.DataContext = _viewModel;            
        }

        private void ScreenSaver_Unloaded(object sender, RoutedEventArgs e) {
            this.DataContext = null;
            this.Unloaded -= ScreenSaver_Unloaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e) {
            _viewModel.ProcsFiltered.CollectionChanged += WhiteList_CollectionChanged;
            UpdateWhiteListEmptyState();
            _ = _viewModel.ListenForClients();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e) {
            _viewModel.ProcsFiltered.CollectionChanged -= WhiteList_CollectionChanged;
            _viewModel.StopListenForClients();
        }

        

        private void RightClickMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = (sender as FrameworkElement).DataContext;
            var procInfo = item as ProcInfo;
            _viewModel.RemoveFromWhiteScr(procInfo);
        }

        private void RemoveWhiteListItem_Click(object sender, RoutedEventArgs e) {
            if (sender is FrameworkElement { DataContext: ProcInfo procInfo }) {
                _viewModel.RemoveFromWhiteScr(procInfo);
            }
        }

        private void WhiteList_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
            UpdateWhiteListEmptyState();
        }

        private void UpdateWhiteListEmptyState() {
            bool hasApps = _viewModel.ProcsFiltered.Count > 0;
            FilteredListView.Visibility = hasApps ? Visibility.Visible : Visibility.Collapsed;
            WhiteListEmptyState.Visibility = hasApps ? Visibility.Collapsed : Visibility.Visible;
            bool isSearching = !string.IsNullOrEmpty(TargetProName.Text);
            EmptyListTitle.Text = LanguageUtil.GetI18n(
                isSearching ? "ScreenSaver_WhiteListNoResults" : "ScreenSaver_WhiteListEmpty");
            EmptyListDescription.Text = LanguageUtil.GetI18n(
                isSearching ? "ScreenSaver_WhiteListNoResultsDescription" : "ScreenSaver_WhiteListEmptyDescription");
        }

        // Whenever text changes in any of the filtering text boxes, the following function is called:
        private void OnFilterChanged(object sender, TextChangedEventArgs e) {
            // This is a Linq query that selects only items that return True after being passed through
            // the Filter function, and adds all of those selected items to filtered.
            var filtered = _viewModel._whiteListScr.Where(Filter);
            Remove_NonMatching(filtered);
            AddBack_Procs(filtered);
            UpdateWhiteListEmptyState();
        }

        /* When the text in any filter is changed, perform a check on each item in the original
        proc list to see if the item should be displayed, taking into account all three of the
        filters currently applied. If the item passes all three checks for all three filters,
        the function returns true and the item is added to the filtered list above. */
        private bool Filter(ProcInfo proc) {
            return proc.ProcName.Contains(TargetProName.Text, StringComparison.InvariantCultureIgnoreCase);
        }

        /* These functions go through the current list being displayed (procsFiltered), and remove
        any items not in the filtered collection (any items that don'T belong), or add back any items
        from the original allProcs list that are now supposed to be displayed (i.e. when backspace is hit). */
        private void Remove_NonMatching(IEnumerable<ProcInfo> procInfos) {
            for (int i = _viewModel.ProcsFiltered.Count - 1; i >= 0; i--) {
                var item = _viewModel.ProcsFiltered[i];
                // If proc is not in the filtered argument list, remove it from the ListView's source.
                if (!procInfos.Contains(item)) {
                    _viewModel.ProcsFiltered.Remove(item);
                }
            }
        }

        private void AddBack_Procs(IEnumerable<ProcInfo> procInfos) {
            foreach (var item in procInfos) {
                // If item in filtered list is not currently in ListView's source collection, add it back in
                if (!_viewModel.ProcsFiltered.Contains(item)) {
                    _viewModel.ProcsFiltered.Add(item);
                }
            }
        }

        private readonly ScreenSaverViewModel _viewModel;
    }
}
