using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.DraftPanel.Views.WorkSpaceComponents;
using VirtualPaper.Models.Mvvm;
using Windows.System;

namespace VirtualPaper.DraftPanel.ViewModels {
    public partial class WorkSpaceViewModel : ObservableObject {
        internal ObservableCollection<TabViewItem> TabViewItems { get; set; } = [];

        private int _selectedTabItemIndex = -1;
        public int SelectedTabItemIndex {
            get { return _selectedTabItemIndex; }
            set { _selectedTabItemIndex = value; OnPropertyChanged(); }
        }

        public WorkSpaceViewModel() {
            TabViewItems.CollectionChanged += TabViewItems_CollectionChanged;
        }

        public MenuBarItem NewMenuBarItem(string title, VirtualKeyModifiers modifiers = VirtualKeyModifiers.None, VirtualKey key = VirtualKey.None) {
            MenuBarItem menuBarItem = new() {
                Title = title,
                KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden,
            };
            menuBarItem.KeyboardAccelerators.Add(new KeyboardAccelerator() {
                Modifiers = modifiers,
                Key = key,
            });

            return menuBarItem;
        }

        private void TabViewItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            SelectedTabItemIndex = e.NewItems.Count - 1;
        }

        internal async void AddDraftItem() {
            //var dialogRes = await _draftPanel.GetDialog().ShowDialogAsync(
            //    new WallpaperCreateView(wpCreateDialogViewModel),
            //    LanguageUtil.GetI18n(Constants.I18n.Dialog_Title_CreateType),
            //    LanguageUtil.GetI18n(Constants.I18n.Text_Confirm),
            //    LanguageUtil.GetI18n(Constants.I18n.Text_Cancel));
            //if (dialogRes != DialogResult.Primary) return;
        }

        internal async Task InitDraftItemAsync(string[] filePaths) {
            if (filePaths == null) return;

            foreach (var filePath in filePaths) {
                if (Path.GetExtension(filePath) == FileFilter.FileExtensions[FileType.FDesign][0]) {
                    var draftMd = await JsonStorage.LoadAsync<DraftMetadata>(filePath, DraftMetadataContext.Default);
                    foreach (var proj in draftMd.Projects) {
                        string projFilePath = Path.Combine(Path.GetDirectoryName(filePath), proj.Name, proj.Name + FileFilter.FileExtensions[FileType.FProject][0]);
                        var projMd = await JsonStorage.LoadAsync<ProjectMetadata>(projFilePath, ProjectMetadataContext.Default);
                        TabViewItems.Add(new() {
                            Header = projMd.Name,
                            Content = new ProjectRun(projFilePath)
                        });
                    }
                }
                else {
                    TabViewItems.Add(new() {
                        Header = Path.GetFileName(filePath),
                        Content = new ProjectRun(filePath)
                    });
                }
            }
        }

        internal void Save() {
            if (SelectedTabItemIndex < 0) return;
            (TabViewItems[SelectedTabItemIndex].Content as ProjectRun)?.Save();
        }

        internal void SaveAll() {
            foreach (var item in TabViewItems) {
                (item.Content as ProjectRun)?.Save();
            }
        }

        internal void Exit() {
            foreach (var item in TabViewItems) {
                (item.Content as ProjectRun)?.Exit();
            }
        }

        internal ObservableCollection<MenuBarItem> _middleMenuItems;
    }
}
