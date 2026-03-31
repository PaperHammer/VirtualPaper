using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.Draft;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Common.Utils.UndoRedo.Events;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Navigation;
using VirtualPaper.UIComponent.Navigation.TabView;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.StaticImg;
using Workloads.Creation.StaticImg.Models.SerializableData;

namespace VirtualPaper.DraftPanel.ViewModels {
    public partial class WorkSpaceViewModel : ObservableObject, IDisposable {
        public ObservableCollection<ArcTabViewItem> TabViewItems { get; set; } = [];

        int _selectedTabIndex = -1;
        public int SelectedTabIndex {
            get { return _selectedTabIndex; }
            set { if (_selectedTabIndex == value) return; _selectedTabIndex = value; OnPropertyChanged(); }
        }

        public WorkSpaceViewModel(IUserSettingsClient userSettings) {
            this._userSettings = userSettings;
        }

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
            return targetItem?.Tag is IRuntime runtime
                ? command(runtime)
                : Task.CompletedTask;
        }
        #endregion

        #region project        
        internal async Task AddNewItemsAsync(PreProjectData[]? predatas) {
            if (predatas == null || predatas.Length == 0) return;

            foreach (var data in predatas) {
                try {
                    // 判断是物理路径(打开现有)还是纯名称(新建)
                    bool isFilePath = Path.IsPathRooted(data.Identity) || File.Exists(data.Identity);

                    if (isFilePath) {
                        if (!File.Exists(data.Identity)) {
                            GlobalMessageUtil.ShowError(
                                message: nameof(Constants.I18n.Project_SI_FileNotFound),
                                isNeedLocalizer: true,
                                extraMsg: data.Identity);
                            continue;
                        }

                        if (FileUtil.IsValidFilePath(data.Identity)) {
                            await InitRuntimeItemWithFileAsync(data.Identity);
                        }
                    }
                    else {
                        if (FileUtil.IsValidFileName(data.Identity)) {
                            InitRuntimeItemWithIdentify(data.Identity, data.Type);
                        }
                    }
                }
                catch (Exception ex) {
                    // 单个文件加载失败不影响其他文件
                    ArcLog.GetLogger<WorkSpaceViewModel>().Error($"Failed to process project item: {data.Identity}", ex);
                    GlobalMessageUtil.ShowException(ex);
                }
            }

            if (!_tempRecentUsed.IsEmpty) {
                await _userSettings.UpdateRecetUsedAsync(_tempRecentUsed.ToArray());
            }
        }

        private async Task InitRuntimeItemWithFileAsync(string filePath) {
            string extension = Path.GetExtension(filePath);
            FileType rtFileType = FileFilter.GetRuntimeFileType(extension);

            switch (rtFileType) {
                case FileType.FImage:
                    // var runtime = new Workloads.Creation.StaticImg.MainPage(Draft.Instance, filePath, rtFileType);
                    // AddToWorkSpace(filePath, runtime, true); // true 表示来自现有文件
                    break;
                //case FileType.FGif:
                //    break;
                //case FileType.FVideo:
                //    break;
                case FileType.FDesign:
                    var isSuccess = await ReadDesignFileAsync(filePath);
                    if (isSuccess && !_tempRecentUsed.Contains(filePath)) {
                        _tempRecentUsed.Add(filePath);
                    }
                    break;
                default:
                    break;
            }
        }

        private void InitRuntimeItemWithIdentify(string fileName, ProjectType type) {
            switch (type) {
                case ProjectType.P_StaticImage:
                    AddToWorkSpace(fileName, false); // false 表示新建未保存
                    break;

                default:
                    break;
            }
        }

        private async Task<bool> ReadDesignFileAsync(string filePath) {
            var result = await StaticImgDesignFileUtil.GetFileHeaderAsync(filePath);
            if (result is not FileHeader header) {
                return false;
            }

            switch (header.ProjType) {
                case ProjectType.P_StaticImage:
                    AddToWorkSpace(filePath, true); // true 表示来自现有文件
                    break;

                default:
                    break;
            }

            return true;
        }

        private void AddToWorkSpace(string file, bool isFromFile) {
            CrossThreadInvoker.InvokeOnUIThread(() => {
                var runtime = new Workloads.Creation.StaticImg.MainPage(file);
                runtime.IsSavedChanged += Runtime_IsSavedChanged;

                var header = new ArcTabViewItemHeader() {
                    MainContent = new TextBlock {
                        Text = Path.GetFileName(file),
                        TextTrimming = TextTrimming.CharacterEllipsis, // 文本超出时显示省略号
                        MaxWidth = 200
                    },
                    IsSaved = isFromFile, // 来自文件则初始化为已保存，新建则为未保存
                };

                var tabItem = new ArcTabViewItem() {
                    Header = header,
                    Tag = runtime,
                };
                TabViewItems.Add(tabItem);
                _runtimeToArcTab[runtime] = (header, tabItem);
            });
        }

        private void Runtime_IsSavedChanged(object? sender, IsSavedChangedEventArgs e) {
            if (sender is IRuntime runtime && _runtimeToArcTab.TryGetValue(runtime, out var value)) {
                value.Header.IsSaved = e.IsSaved;
            }
        }
        #endregion

        #region dispose
        private bool _isDisposed;

        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    _runtimeToArcTab.Clear();
                }
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        internal async Task<bool> CheckSaveStatusAsync(IRuntime runtime) {
            var header = _runtimeToArcTab[runtime].Header;

            if (!header.IsSaved) {
                var res = await GlobalDialogUtils.ShowDialogAsync(
                    content: $"{(runtime as MainPage)?.Session.DesignFileUtil.FileName} {Constants.I18n.Project_Unsave_Intercept_Content}",
                    title: $"{Constants.I18n.Project_Unsave_Intercept_Title}",
                    primaryBtnText: $"{Constants.I18n.Text_Save}",
                    secondaryBtnText: $"{Constants.I18n.Text_Unsave}",
                    closeBtnText: $"{Constants.I18n.Text_Cancel}");

                if (res == DialogResult.Primary) {
                    bool isSuccess = await runtime.SaveAsync();
                    return isSuccess;
                }
                else if (res == DialogResult.None) {
                    return false;
                }
            }

            CloseWorkSpaceTab(runtime);
            return true;
        }

        internal async Task<bool> CheckAllSaveStatusAsync() {
            foreach (var kvp in _runtimeToArcTab) {
                var runtime = kvp.Key;
                var header = kvp.Value.Header;

                if (!header.IsSaved) {
                    var res = await GlobalDialogUtils.ShowDialogAsync(
                        content: $"\"{(runtime as Workloads.Creation.StaticImg.MainPage)?.Session.DesignFileUtil.FileName}\" {LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Unsave_Intercept_Content))}",
                        title: $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Unsave_Intercept_Title))}",
                        primaryBtnText: $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Save))}",
                        secondaryBtnText: $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Unsave))}",
                        closeBtnText: $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Cancel))}"
                    );

                    if (res == DialogResult.Primary) {
                        bool isSuccess = await runtime.SaveAsync();
                        if (!isSuccess) return false;
                    }
                    else if (res == DialogResult.None) {
                        return false;
                    }
                }

                CloseWorkSpaceTab(runtime);
            }

            return true;
        }

        private void CloseWorkSpaceTab(IRuntime runtime) {
            runtime.IsSavedChanged -= Runtime_IsSavedChanged;
            _runtimeToArcTab.Remove(runtime);
        }

        internal IRuntime? GetSelectedItem() {
            if (SelectedTabIndex < 0 || SelectedTabIndex >= TabViewItems.Count) return null;
            return TabViewItems[SelectedTabIndex].Tag as IRuntime;
        }
        #endregion

        internal readonly ObservableCollection<MenuBarItem> _middleMenuItems = [];
        private readonly IUserSettingsClient _userSettings;
        private readonly ConcurrentBag<string> _tempRecentUsed = [];
        private readonly Dictionary<IRuntime, (ArcTabViewItemHeader Header, ArcTabViewItem Item)> _runtimeToArcTab = [];
    }
}
