using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Models.DraftPanel;
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.Extensions;

// To learn more about WinUI, the WinUI draft structure,
// and more about our draft templates, see: http://aka.ms/winui-draft-info.

namespace VirtualPaper.DraftPanel.Views.ConfigSpaceComponents {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GetStart : Page {
        public GetStart() {
            this.InitializeComponent();
            _viewModel = ObjectProvider.GetRequiredService<GetStartViewModel>();
            this.DataContext = _viewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (e.Parameter is NavigationPayload payload) {
                if (payload.TryGet(NaviPayloadKey.ConfigSpacePage.ToString(), out _configSpace)) {
                    this._configSpace?.SetBtnVisible(false);
                }
            }
        }

        private void OnFilterChanged(object sender, TextChangedEventArgs e) {
            _viewModel.ApplyFilter(tbSearchName.Text);
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
                        _viewModel.RemoveFromListCommand?.Execute(e.ClickedItem);
                    }
                    return;
                }

                var payloadDatas = new NaviPayloadData[] {
                    new(NaviPayloadKey.RecentUsedFiles, ru.FilePath)
                };
                _configSpace?.NavigateByState(DraftPanelState.WorkSpace, payloadDatas);
            }
        }

        private void BtnStartupNew_Click(object sender, RoutedEventArgs e) {
            _configSpace?.NavigateByState(DraftPanelState.DraftConfig);
        }

        private async void BtnStartupOpen_Click(object sender, RoutedEventArgs e) {
            var storage = await WindowsStoragePickers.PickFilesAsync(
                WindowConsts.WindowHandle,
                [.. FileFilter.FileTypeToExtension[FileType.FImage], .. FileFilter.FileTypeToExtension[FileType.FDesign]],
                true);
            if (storage == null || storage.Length < 1) return;

            int n = storage.Length;
            string[] filePaths = new string[n];
            for (int i = 0; i < storage.Length; i++) {
                filePaths[i] = storage[i].Path;
            }

            var payloadDatas = new NaviPayloadData[] {
                new(NaviPayloadKey.LocalFiles, filePaths)
            };
            _configSpace?.NavigateByState(DraftPanelState.WorkSpace, payloadDatas);
        }

        // todo
        private void GridDrop_Drop(object sender, DragEventArgs e) {

        }

        private void GridDrop_DragOver(object sender, DragEventArgs e) {

        }

        private readonly GetStartViewModel _viewModel;
        private ConfigSpace? _configSpace;
    }
}
