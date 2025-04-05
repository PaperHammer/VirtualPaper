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

        int _selectedTabIndex;
        public int SelectedTabIndex {
            get { return _selectedTabIndex; }
            set { if (_selectedTabIndex == value) return; _selectedTabIndex = value; OnPropertyChanged(); }
        }

        public WorkSpaceViewModel() {
            //TabViewItems.CollectionChanged += TabViewItems_CollectionChanged;
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
            // 如果集合为空，则取消选择
            if (TabViewItems.Count == 0) {
                SelectedTabIndex = -1;
                return;
            }

            switch (e.Action) {
                case NotifyCollectionChangedAction.Add:
                    // 如果当前没有选中的tab，则选中新添加的第一个tab
                    if (SelectedTabIndex == -1 && e.NewItems?.Count > 0) {
                        SelectedTabIndex = 0;
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    // 若被移除的项包含当前选中项，则需要更新选中项
                    if (e.OldItems?.Contains(TabViewItems[SelectedTabIndex]) == true) {
                        // 使用被移除项的起始索引来决定下一个选中项
                        int newIndex = e.OldStartingIndex;
                        if (newIndex >= TabViewItems.Count) {
                            newIndex = TabViewItems.Count - 1;
                        }
                        SelectedTabIndex = newIndex;
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    // 重置时设置第一个tab为当前选中项
                    SelectedTabIndex = TabViewItems.Count > 0 ? 0 : -1;
                    break;
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Move:
                    // 其它动作不更新选中逻辑
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
                            runtime = new StaticImg(entryFilePath, FileType.FProject); // xxx.vproj
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
