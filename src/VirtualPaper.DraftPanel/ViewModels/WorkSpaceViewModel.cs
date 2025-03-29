using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.DraftPanel.Model.Interfaces;
using VirtualPaper.DraftPanel.Model.NavParam;
using VirtualPaper.DraftPanel.Panels;
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

        private void TabViewItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
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

        internal async Task SaveAsync() {
            if (SelectedTabItemIndex < 0) return;
            await (TabViewItems[SelectedTabItemIndex].Content as IRuntime)?.SaveAsync();
        }

        internal async Task SaveAllAsync() {
            foreach (var item in TabViewItems) {
                await (item.Content as IRuntime)?.SaveAsync();
            }
        }

        internal async Task ExitAsync() {
            await SaveAllAsync();
        }

        internal void InitTabViewItems(ToWorkSpace data) {
            foreach (var filePath in data.FilePaths) {
                InitRuntimeItemAsync(filePath);
            }
        }

        private async Task InitRuntimeItemAsync(string filePath) {
            string extension = Path.GetExtension(filePath);
            FileType rtFileType = FileFilter.GetRuntimeFileType(extension);

            IRuntime runtime;
            switch (rtFileType) {
                case FileType.FUnknown:
                    break;
                case FileType.FImage:
                    runtime = new StaticImg(filePath, rtFileType); // xxx.jpg[etc.]
                    AddToWorkSpace(filePath, runtime);
                    break;
                case FileType.FGif:
                    break;
                case FileType.FVideo:
                    break;
                case FileType.FDesign:
                    await ReadDraftFileAsync(filePath); // [folder]/xxx.vpd
                    break;
                case FileType.FProject:
                    await ReadProjectFileAsync(filePath); // [folder]/xxx.vproj
                    break;
                default:
                    break;
            }
        }

        private async Task ReadDraftFileAsync(string filePath) {
            try {
                var draftMd = await DraftMetadata.LoadAsync(filePath);
                foreach (var projTag in draftMd.ProjectTags) {
                    string projFilePath = Path.Combine(Path.GetDirectoryName(filePath), projTag.RelativePath);
                    await ReadProjectFileAsync(projFilePath);
                }
            }
            catch (Exception ex) {
                Draft.Instance.GetNotify().ShowExp(ex);
            }
        }

        private async Task ReadProjectFileAsync(string projFilePath) {
            try {
                var projData = await ProjectMetadata.LoadAsync(projFilePath);
                string entryFilePath = Path.Combine(Path.GetDirectoryName(projFilePath), projData.EntryRelativePath);

                IRuntime runtime;
                switch (projData.Type) {
                    case ProjectType.PImage:
                        runtime = new StaticImg(entryFilePath, FileType.FProject); // xxx.simd
                        AddToWorkSpace(entryFilePath, runtime);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex) {
                Draft.Instance.GetNotify().ShowExp(ex);
            }
        }

        private void AddToWorkSpace(string filePath, IRuntime runtime) {
            _rt.Add(runtime);
            TabViewItems.Add(new() {
                Header = new TextBlock {
                    Text = Path.GetFileName(filePath),
                    TextTrimming = TextTrimming.CharacterEllipsis, // 文本超出时显示省略号
                    MaxWidth = 200
                },
                Content = runtime
            });
        }

        internal readonly ObservableCollection<MenuBarItem> _middleMenuItems = [];
        private readonly List<IRuntime> _rt = [];
    }
}
