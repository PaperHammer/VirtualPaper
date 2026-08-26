using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Models.DraftPanel;
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Data;
using VirtualPaper.UIComponent.Utils;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Workloads.Utils.DraftUtils.Interfaces;

// To learn more about WinUI, the WinUI draft structure,
// and more about our draft templates, see: http://aka.ms/winui-draft-info.

namespace VirtualPaper.DraftPanel.Views.ConfigSpaceComponents {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GetStart : Page, ICardComponent {
        public string PreviousStepBtnText => _viewModel.PreviousStepBtnText;
        public bool BtnVisible => _viewModel.BtnVisible;
        public bool IsNextStepVisible => false;
        public Action? CardUIStateChanged {
            get => _viewModel.CardUIStateChanged;
            set => _viewModel.CardUIStateChanged = value;
        }
        public Func<object?, Task>? PreviousStepAction => async (_) => await _viewModel.OnPreviousStepClickedAsync();

        public GetStart() {
            this.InitializeComponent();
            _viewModel = AppServiceLocator.Services.GetRequiredService<GetStartViewModel>();
            this.DataContext = _viewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (e.Parameter is FrameworkPayload payload) {
                payload.TryGet(NaviPayloadKey.INavigateComponent, out _navigateComponent);
                _viewModel.DraftConfigTCS = payload.Get<TaskCompletionSource<PreProjectData[]?>>(NaviPayloadKey.DraftConfigTCS);
                _viewModel.IsFromWorkSpaceForAddProj = payload.Get<bool>(NaviPayloadKey.IsFromWorkSpaceForAddProj);
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e) {
            _viewModel.InitCollection();
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

                OpenProjects([new(ru.FilePath, ProjectType.P_StaticImage, ProjectOpenIntent.OpenExisting)]);
            }
        }

        private void BtnStartupNew_Click(object sender, RoutedEventArgs e) {
            _navigateComponent?.NavigateByState(DraftPanelState.DraftConfig);
        }

        private async void BtnStartupOpen_Click(object sender, RoutedEventArgs e) {
            var storage = await WindowsStoragePickers.PickFilesAsync(
                WindowConsts.WindowHandle,
                [.. FileFilter.FileTypeToExtension[FileType.FDesign], .. FileFilter.FileTypeToExtension[FileType.FWebDesign], .. FileFilter.FileTypeToExtension[FileType.FImage]],
                true);
            OpenLocalFiles(storage);
        }

        private void OpenLocalFiles(IReadOnlyList<IStorageItem> items) {
            if (items == null || items.Count < 1) return;

            int n = items.Count;
            PreProjectData[] datas = new PreProjectData[n];
            for (int i = 0; i < items.Count; i++) {
                datas[i] = new PreProjectData(items[i].Path, ProjectType.P_StaticImage, ProjectOpenIntent.OpenExisting);
            }

            OpenProjects(datas);
        }

        private async void GridDrop_Drop(object sender, DragEventArgs e) {
            if (e.DataView.Contains(StandardDataFormats.StorageItems)) {
                var items = await e.DataView.GetStorageItemsAsync();
                var allowedExtensions = FileFilter.FileTypeToExtension[FileType.FImage]
                    .Concat(FileFilter.FileTypeToExtension[FileType.FDesign])
                    .Concat(FileFilter.FileTypeToExtension[FileType.FWebDesign]);

                var filteredItems = items
                    .OfType<IStorageFile>()
                    .Where(file => allowedExtensions.Contains(file.FileType.ToLower()))
                    .Cast<IStorageItem>()
                    .ToList();

                if (filteredItems.Count != 0) {
                    OpenLocalFiles(filteredItems);
                }
                else {
                    GlobalMessageUtil.ShowWarning(
                        message: nameof(Constants.I18n.Project_Drops_Contains_Invalid_FIles),
                        isNeedLocalizer: true);
                }
            }
        }

        private void GridDrop_DragOver(object sender, DragEventArgs e) {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        public void UpdateCardComponentUI() {
            _viewModel.UpdateCardComponentUI();
        }

        private void OpenProjects(PreProjectData[] datas) {
            if (_viewModel.IsFromWorkSpaceForAddProj) {
                _viewModel.DraftConfigTCS?.TrySetResult(datas);
                return;
            }

            _navigateComponent.GetPaylaod()?.Set(NaviPayloadKey.Project, datas);
            _navigateComponent.NavigateByState(DraftPanelState.WorkSpace);
        }

        private readonly GetStartViewModel _viewModel;
        private INavigateComponent _navigateComponent = null!;
    }
}
