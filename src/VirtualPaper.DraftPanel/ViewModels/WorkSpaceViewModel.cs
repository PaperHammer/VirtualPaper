using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Octokit;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.DraftPanel.Views.WorkSpaceComponents;
using VirtualPaper.UIComponent.Others;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.DraftPanel.ViewModels {
    public class WorkSpaceViewModel {
        public ObservableCollection<TabViewItem> TabViewItems { get; set; } = [];

        internal async void InitContent() {
            string storageFolder = _draftPanel.GetParam() as string ?? string.Empty;
            if (storageFolder == string.Empty) return;

            _draftPanel.GetNotify().Loading(false, false);

            string draftFilePath = Path.Combine(storageFolder, Path.GetFileName(storageFolder) + ".vpd");
            var dm = await JsonStorage<DraftMetadata>.LoadDataAsync(draftFilePath, DraftMetadataContext.Default);

            if (dm.Projects != null) {
                foreach (var item in dm.Projects) {
                    TabViewItems.Add(new() {
                        Header = item.Name,
                        Content = new ProjectRun(item)
                    });
                }
            }

            _draftPanel.GetNotify().Loaded();
        }

        internal async Task AddDraftItemAsync() {
            //var dialogRes = await _draftPanel.GetDialog().ShowDialogAsync(
            //    new WallpaperCreateView(wpCreateDialogViewModel),
            //    LanguageUtil.GetI18n(Constants.I18n.Dialog_Title_CreateType),
            //    LanguageUtil.GetI18n(Constants.I18n.Text_Confirm),
            //    LanguageUtil.GetI18n(Constants.I18n.Text_Cancel));
            //if (dialogRes != DialogResult.Primary) return;
        }

        internal IDraftPanelBridge _draftPanel;
    }
}
