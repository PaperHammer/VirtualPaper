using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.Draft;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Navigation;
using VirtualPaper.UIComponent.Navigation.TabView;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.StaticImg.Models.SerializableData;

namespace VirtualPaper.DraftPanel.ViewModels {
    public partial class WorkSpaceViewModel : ObservableObject, IDisposable {
        internal ObservableCollection<ArcTabViewItem> TabViewItems { get; set; } = [];

        int _selectedTabIndex = -1;
        public int SelectedTabIndex {
            get { return _selectedTabIndex; }
            set { if (_selectedTabIndex == value) return; _selectedTabIndex = value; OnPropertyChanged(); }
        }

        public WorkSpaceViewModel(IUserSettingsClient userSettings) {
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

        #region ui events
        internal async void AddDraftItem() {
            //var dialogRes = await _draftPanel.GetDialog().ShowDialogAsync(
            //    new WallpaperCreateView(wpCreateDialogViewModel),
            //    LanguageUtil.GetI18n(Constants.I18n.Dialog_Title_CreateType),
            //    LanguageUtil.GetI18n(Constants.I18n.Text_Confirm),
            //    LanguageUtil.GetI18n(Constants.I18n.Text_Cancel));
            //if (dialogRes != DialogResult.Primary) return;
        }

        internal async Task SaveAsync() => await ExecuteRuntimeCommandAsync(x => x.SaveAsync());

        internal async Task SaveAllAsync() => await Task.WhenAll(
            TabViewItems.Select(item => ExecuteRuntimeCommandAsync(x => x.SaveAsync(), item)));

        internal async Task ExitAsync() {
            await SaveAllAsync();
        }

        internal async Task UndoAsync() => await ExecuteRuntimeCommandAsync(x => x.UndoAsync());

        internal async Task RedoAsync() => await ExecuteRuntimeCommandAsync(x => x.RedoAsync());

        private Task ExecuteRuntimeCommandAsync(Func<IRuntime, Task> command, TabViewItem? specificItem = null) {
            var targetItem = specificItem ?? TabViewItems.ElementAtOrDefault(SelectedTabIndex);
            return targetItem?.Content is IRuntime runtime
                ? command(runtime)
                : Task.CompletedTask;
        }
        #endregion

        #region init
        internal async Task InitTabViewItems(PreProjectData[] predatas) {
            using var semaphore = new SemaphoreSlim(5);

            var tasks = new List<Task>();
            foreach (var data in predatas) {
                bool isFilePath = Path.IsPathRooted(data.Identity) || File.Exists(data.Identity);

                if (isFilePath) {
                    if (FileUtil.IsValidFilePath(data.Identity)) {
                        tasks.Add(Task.Run(async () => {
                            await semaphore.WaitAsync();
                            try {
                                await InitRuntimeItemAsync(data.Identity);
                            }
                            finally {
                                semaphore.Release();
                            }
                        }));
                    }
                }
                else {
                    if (FileUtil.IsValidFileName(data.Identity)) {
                        InitRuntimeItem(data.Identity, data.Type);
                    }
                }
            }

            await Task.WhenAll(tasks);
            await _userSettings.UpdateRecetUsedAsync(_tempRecentUsed.ToArray());
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
                //case FileType.FGif:
                //    break;
                //case FileType.FVideo:
                //    break;
                case FileType.FDesign:
                    var flag = await ReadDesignFileAsync(filePath); // [folder]/xxx.vpd
                    if (flag) {
                        _tempRecentUsed.Add(filePath);
                    }
                    break;
                default:
                    break;
            }
        }

        private void InitRuntimeItem(string fileName, ProjectType type) {
            try {
                IRuntime runtime;
                switch (type) {
                    case ProjectType.P_StaticImage:
                        CrossThreadInvoker.InvokeOnUIThread(() => {
                            runtime = new Workloads.Creation.StaticImg.MainPage(fileName);
                            AddToWorkSpace(fileName, runtime);
                        });
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WorkSpaceViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
            }
        }

        private async Task<bool> ReadDesignFileAsync(string filePath) {
            try {
                if (!File.Exists(filePath)) {
                    GlobalMessageUtil.ShowError(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)),
                        message: nameof(Constants.I18n.Project_SI_FileNotFound),
                        isNeedLocalizer: true,
                        extraMsg: filePath);
                    return false;
                }

                var result = await StaticImgDesignFileUtil.GetFileHeaderAsync(filePath);
                if (result is not FileHeader header) {
                    return false;
                }

                IRuntime runtime;
                switch (header.ProjType) {
                    case ProjectType.P_StaticImage:
                        CrossThreadInvoker.InvokeOnUIThread(() => {
                            runtime = new Workloads.Creation.StaticImg.MainPage(filePath); // xxx.vpd
                            AddToWorkSpace(filePath, runtime);
                        });
                        break;
                    default:
                        break;
                }

                return true;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<WorkSpaceViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
            }

            return false;
        }

        private void AddToWorkSpace(string filePath, IRuntime runtime) {
            _rt.Add(runtime);
            TabViewItems.Add(new ArcTabViewItem() {
                Header = new ArcTabViewItemHeader() {
                    MainContent = new TextBlock {
                        Text = Path.GetFileName(filePath),
                        TextTrimming = TextTrimming.CharacterEllipsis, // 文本超出时显示省略号
                        MaxWidth = 200
                    },
                    IsUnsaved = false,
                },
                Content = runtime,
            });
        }
        #endregion

        #region dispose
        private bool _isDisposed;

        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    TabViewItems?.Clear();
                    _middleMenuItems.Clear();
                    _rt.Clear();
                }
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        internal readonly ObservableCollection<MenuBarItem> _middleMenuItems = [];
        private readonly List<IRuntime> _rt = [];
        private readonly IUserSettingsClient _userSettings;
        private readonly ConcurrentBag<string> _tempRecentUsed = [];
    }
}
