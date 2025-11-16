using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.DraftPanel.Model.NavParam;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.DraftPanel;
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Utils;

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

            this._configSpace ??= e.Parameter as ConfigSpace;
            this._configSpace?.SetBtnVisible(false);

            _viewModel = ObjectProvider.GetRequiredService<GetStartViewModel>(lifetimeForParams: ObjectLifetime.Singleton);
            this.DataContext = _viewModel;            
        }

        private void OnFilterChanged(object sender, TextChangedEventArgs e) {
            var filtered = _viewModel._recentUsed.Where(Filter);
            Remove_NonMatching(filtered);
            AddBack_Procs(filtered);
        }

        private bool Filter(IRecentUsed recentUsed) {
            return recentUsed.FileName.Contains(tbSearchName.Text, StringComparison.InvariantCultureIgnoreCase);
        }

        private void Remove_NonMatching(IEnumerable<IRecentUsed> recentuseds) {
            for (int i = _viewModel.RecentUseds.Count - 1; i >= 0; i--) {
                var item = _viewModel.RecentUseds[i];
                if (!recentuseds.Contains(item)) {
                    _viewModel.RecentUseds.Remove(item);
                }
            }
        }

        private void AddBack_Procs(IEnumerable<IRecentUsed> recentuseds) {
            foreach (var item in recentuseds) {
                if (!_viewModel.RecentUseds.Contains(item)) {
                    _viewModel.RecentUseds.Add(item);
                }
            }
        }

        private async void RecentUsedsListView_ItemClick(object sender, ItemClickEventArgs e) {
            if (e.ClickedItem is RecentUsed ru) {
                if (!Path.Exists(ru.FilePath)) {
                    var diaRes = await GlobalDialogUtils.ShowDialogWithoutTitleAsync(
                        LanguageUtil.GetI18n(nameof(Constants.I18n.Project_SI_FileNotFound)),
                        LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Confirm)),
                        LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Cancel))
                    );
                    if (diaRes == DialogResult.Primary) {
                        _viewModel.RemoveFromListCommand.Execute(e.ClickedItem);
                    }
                    return;
                }

                _configSpace.ChangePanelState(DraftPanelState.WorkSpace, new ToWorkSpace([ru.FilePath]));
            }
        }

        //private async Task HandleStartupAsync(Startup startUp) {
        //    foreach (var stg in _strategies) {
        //        if (stg.CanHandle(startUp.Type)) {
        //            await stg.HandleAsync(_configSpace);
        //            break;
        //        }
        //    }
        //}

        //private void FocusOnFirstItem() {
        //    if (lvRecentUsed.Items.Count > 0) {
        //        var firstItemContainer = lvRecentUsed.ContainerFromIndex(0) as ListViewItem;
        //        firstItemContainer?.Focus(FocusState.Programmatic);
        //    }
        //}

        private void BtnStartupNew_Click(object sender, RoutedEventArgs e) {
            _configSpace.ChangePanelState(DraftPanelState.DraftConfig, null);
        }

        private async void BtnStartupOpen_Click(object sender, RoutedEventArgs e) {
            var storage = await WindowsStoragePickers.PickFilesAsync(
                WindowConsts.WindowHandle,
                [.. FileFilter.FileTypeToExtension[FileType.FImage], .. FileFilter.FileTypeToExtension[FileType.FDesign]],
                true);
            if (storage.Length < 1) return;

            int n = storage.Length;
            string[] filePaths = new string[n];
            for (int i = 0; i < storage.Length; i++) {
                filePaths[i] = storage[i].Path;
            }
            _configSpace.ChangePanelState(DraftPanelState.WorkSpace, new ToWorkSpace([.. filePaths]));
        }

        private void GridDrop_Drop(object sender, DragEventArgs e) {

        }

        private void GridDrop_DragOver(object sender, DragEventArgs e) {

        }

        private GetStartViewModel _viewModel;
        private ConfigSpace _configSpace;
        //private readonly IStrategy[] _strategies = [
        //    new OpenVpd(),
        //    new OpenFile(),
        //    new NewVpd(),
        //];
        private readonly string _SIG_Text_RemoveFromList = LanguageUtil.GetI18n(nameof(Constants.I18n.SIG_Text_RemoveFromList));
        private readonly string _SIG_Text_CopyPath = LanguageUtil.GetI18n(nameof(Constants.I18n.SIG_Text_CopyPath));
    }
}
