using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Common.Runtime.Draft;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.DraftPanel.Model.NavParam;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Navigation;

namespace VirtualPaper.DraftPanel.ViewModels {
    public partial class WorkSpaceViewModel : ObservableObject {
        internal ObservableCollection<ArcTabViewItem> TabViewItems { get; set; } = [];

        int _selectedTabIndex = -1;
        public int SelectedTabIndex {
            get { return _selectedTabIndex; }
            set { if (_selectedTabIndex == value) return; _selectedTabIndex = value; OnPropertyChanged(); }
        }

        public WorkSpaceViewModel(
            IUserSettingsClient userSettings) {
            this._userSettings = userSettings;
        }

        //public MenuBarItem NewMenuBarItem(string title, VirtualKeyModifiers modifiers = VirtualKeyModifiers.None, VirtualKey key = VirtualKey.None) {
        //    MenuBarItem menuBarItem = new() {
        //        Title = title,
        //        KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden,
        //    };
        //    menuBarItem.KeyboardAccelerators.Add(new KeyboardAccelerator() {
        //        Modifiers = modifiers,
        //        Key = key,
        //    });

        //    return menuBarItem;
        //}

        internal void OnTabItemsChanged(TabView sender, Windows.Foundation.Collections.IVectorChangedEventArgs args) {
            // 如果集合为空，则取消选择
            if (TabViewItems.Count == 0) {
                SelectedTabIndex = -1;
                return;
            }

            switch (args.CollectionChange) {
                case Windows.Foundation.Collections.CollectionChange.ItemInserted:
                    SelectedTabIndex = (int)args.Index;
                    break;
                case Windows.Foundation.Collections.CollectionChange.ItemRemoved:
                    // 如果被移除的是当前选中项
                    if (args.Index == SelectedTabIndex) {
                        // 优先尝试选中前一个选项卡
                        int newIndex = (int)args.Index - 1;

                        // 如果前一个不存在（如删除的是第一个），则尝试选后一个
                        if (newIndex < 0 && TabViewItems.Count > 0) {
                            newIndex = 0;
                        }

                        // 确保索引有效
                        SelectedTabIndex = Math.Clamp(newIndex, -1, TabViewItems.Count - 1);
                    }
                    // 如果被移除项在当前选中项之前，需要调整选中索引
                    else if (args.Index < SelectedTabIndex) {
                        SelectedTabIndex = Math.Clamp(SelectedTabIndex - 1, -1, TabViewItems.Count - 1);
                    }
                    break;
                case Windows.Foundation.Collections.CollectionChange.Reset:
                    // 重置时默认选中第一个选项卡
                    SelectedTabIndex = TabViewItems.Count > 0 ? 0 : -1;
                    break;
                case Windows.Foundation.Collections.CollectionChange.ItemChanged:
                default:
                    break;
            }
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
            if (SelectedTabIndex == -1) return;
            await (TabViewItems[SelectedTabIndex].Content as IRuntime)?.SaveAsync();
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

            //IRuntime runtime;
            switch (rtFileType) {
                case FileType.FUnknown:
                    break;
                case FileType.FImage:
                    //runtime = new Workloads.Creation.StaticImg.MainPage(Draft.Instance, filePath, rtFileType); // xxx.jpg[etc.]
                    //AddToWorkSpace(filePath, runtime);
                    break;
                case FileType.FGif:
                    break;
                case FileType.FVideo:
                    break;
                case FileType.FDesign:
                    await ReadDraftFileAsync(filePath); // [folder]/xxx.vpd
                    await _userSettings.UpdateRecetUsedAsync(filePath);
                    break;
                //case FileType.FProject:
                //     ReadProjectFile(filePath); // [folder]/xxx.vproj
                //    break;
                default:
                    break;
            }
        }

        private async Task ReadDraftFileAsync(string filePath) {
            try {
                var draftMd = await DraftMetadata.LoadAsync(filePath);
                foreach (var projTag in draftMd.ProjectTags) {
                    string entryFilePath = Path.Combine(Path.GetDirectoryName(filePath), projTag.EntryRelativeFilePath);

                    IRuntime runtime;
                    switch (projTag.Type) {
                        case ProjectType.PImage:
                            runtime = new Workloads.Creation.StaticImg.MainPage(Draft.Instance, entryFilePath, FileType.FProject); // xxx.vproj
                            AddToWorkSpace(entryFilePath, runtime);
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception ex) {
                Draft.Instance.GetNotify().ShowExp(ex);
            }
        }

        private void AddToWorkSpace(string filePath, IRuntime runtime) {
            _rt.Add(runtime);
            TabViewItems.Add(new ArcTabViewItem() {
                Header = new TextBlock {
                    Text = Path.GetFileName(filePath),
                    TextTrimming = TextTrimming.CharacterEllipsis, // 文本超出时显示省略号
                    MaxWidth = 200
                },
                Content = runtime,
                IsUnsaved = false,
            });
        }

        internal readonly ObservableCollection<MenuBarItem> _middleMenuItems = [];
        private readonly List<IRuntime> _rt = [];
        private readonly IUserSettingsClient _userSettings;
    }
}
