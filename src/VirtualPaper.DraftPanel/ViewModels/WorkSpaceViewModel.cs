using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Common.Utils.UndoRedo.Events;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Navigation;
using VirtualPaper.UIComponent.Navigation.TabView;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.StaticImg.Models.SerializableData;
using Workloads.Utils.DraftUtils.Interfaces;
using Workloads.Utils.DraftUtils.Models;

namespace VirtualPaper.DraftPanel.ViewModels {
    public partial class WorkSpaceViewModel : ObservableObject, IDisposable {
        public ObservableCollection<ArcTabViewItem> TabViewItems { get; set; } = [];

        int _selectedTabIndex = -1;
        public int SelectedTabIndex {
            get { return _selectedTabIndex; }
            set { if (_selectedTabIndex == value) return; _selectedTabIndex = value; OnPropertyChanged(); }
        }

        public ICommand? MFI_SaveCommand { get; private set; }
        public ICommand? MFI_SaveAllCommand { get; private set; }
        public ICommand? MFI_ExitCommand { get; private set; }
        public ICommand? MFI_UndoCommand { get; private set; }
        public ICommand? MFI_RedoCommand { get; private set; }
        public ICommand? MFI_ManualCommand { get; private set; }
        public ICommand? MFI_AboutCommand { get; private set; }

        public WorkSpaceViewModel(IUserSettingsClient userSettings) {
            this._userSettings = userSettings;
            InitCommand();
        }

        private void InitCommand() {
            MFI_SaveCommand = new RelayCommand(async () => {
                await SaveAsync();
            });
            MFI_SaveAllCommand = new RelayCommand(async () => {
                await SaveAllAsync();
            });
            MFI_UndoCommand = new RelayCommand(async () => {
                await UndoAsync();
            });
            MFI_RedoCommand = new RelayCommand(async () => {
                await RedoAsync();
            });
            MFI_ManualCommand = new RelayCommand(async () => {
                var uri = new Uri("https://github.com/PaperHammer/VirtualPaper/wiki");
                await Windows.System.Launcher.LaunchUriAsync(uri);
            });
            MFI_AboutCommand = new RelayCommand(async () => {
                var uri = new Uri("https://github.com/PaperHammer/VirtualPaper");
                await Windows.System.Launcher.LaunchUriAsync(uri);
            });
        }

        internal void OnTabItemsChanged(TabView sender, Windows.Foundation.Collections.IVectorChangedEventArgs args) {
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
        internal async Task ExportAsync(ExportImageFormat format) => await ExecuteRuntimeCommandAsync(x => x.ExportAsync(format));
        
        private async Task SaveAsync() => await ExecuteRuntimeCommandAsync(x => x.SaveAsync());

        private async Task SaveAllAsync() => await Task.WhenAll(TabViewItems.Select(item => ExecuteRuntimeCommandAsync(x => x.SaveAsync(), item)));

        private async Task UndoAsync() => await ExecuteRuntimeCommandAsync(x => x.UndoAsync());

        private async Task RedoAsync() => await ExecuteRuntimeCommandAsync(x => x.RedoAsync());

        private Task ExecuteRuntimeCommandAsync(Func<IRuntime, Task> command, TabViewItem? specificItem = null) {
            var runtime = (specificItem?.Tag as IRuntime) ?? GetSelectedRuntime();
            return runtime != null
                ? command(runtime)
                : Task.CompletedTask;
        }

        private async Task<T?> ExecuteRuntimeCommandAsync<T>(Func<IRuntime, Task<T>> command, TabViewItem? specificItem = null) {
            var runtime = (specificItem?.Tag as IRuntime) ?? GetSelectedRuntime();
            return runtime != null
                ? await command(runtime)
                : default;
        }

        private async IAsyncEnumerable<T> ExecuteRuntimeCommandStreamAsync<T>(
            Func<IRuntime, IAsyncEnumerable<T>> command,
            TabViewItem? specificItem = null,
            [EnumeratorCancellation] CancellationToken token = default) {
            var runtime = (specificItem?.Tag as IRuntime) ?? GetSelectedRuntime();
            if (runtime != null) {
                await foreach (var item in command(runtime).WithCancellation(token)) {
                    yield return item;
                }
            }
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
                    TabViewItems.Clear();
                    _middleMenuItems.Clear();
                    _tempRecentUsed.Clear();
                    _runtimeToArcTab.Clear();
                    ClearCommand();
                }
                _isDisposed = true;
            }
        }

        private void ClearCommand() {
            MFI_SaveCommand = null;
            MFI_SaveAllCommand = null;
            MFI_ExitCommand = null;
            MFI_UndoCommand = null;
            MFI_RedoCommand = null;
            MFI_ManualCommand = null;
            MFI_AboutCommand = null;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        internal async IAsyncEnumerable<ArcTabViewItem> HandleExitItemsAsync() {
            var tabsToClose = TabViewItems.ToList();

            foreach (var tabItem in tabsToClose) {
                if (tabItem.Tag is not IRuntime runtime) continue;

                bool shouldClose = false;
                if (_runtimeToArcTab.TryGetValue(runtime, out var value)) {
                    var header = value.Header;

                    if (!header.IsSaved) {
                        var res = await GlobalDialogUtils.ShowDialogAsync(
                            content: $"\"{runtime.FileName}\" {LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Unsave_Intercept_Content))}",
                            title: $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Unsave_Intercept_Title))}",
                            primaryBtnText: $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Save))}",
                            secondaryBtnText: $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Unsave))}"
                        );

                        if (res == DialogResult.Primary) {
                            shouldClose = await runtime.SaveAsync();
                        }
                        else if (res == DialogResult.Secondary) {
                            shouldClose = true;
                        }
                    }
                }

                if (shouldClose) {
                    CloseWorkSpaceTab(runtime, tabItem);
                    yield return tabItem;
                }
            }
        }

        internal async Task<bool> CheckSaveStatusAsync(IRuntime runtime) {
            bool flag = false;
            var header = _runtimeToArcTab[runtime].Header;

            if (!header.IsSaved) {
                var res = await GlobalDialogUtils.ShowDialogAsync(
                    content: $"\"{runtime.FileName}\" {LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Unsave_Intercept_Content))}",
                    title: $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Unsave_Intercept_Title))}",
                    primaryBtnText: $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Save))}",
                    secondaryBtnText: $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Unsave))}",
                    closeBtnText: $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Cancel))}");

                if (res == DialogResult.Primary) {
                    flag = await runtime.SaveAsync();
                }
                else if (res == DialogResult.Secondary) {
                    flag = true;
                }
            }

            if (flag) {
                CloseWorkSpaceTab(runtime, _runtimeToArcTab[runtime].Item);
            }
            return flag;
        }

        internal async Task<bool> CheckAllSaveStatusAsync() {
            foreach (var kvp in _runtimeToArcTab) {
                var runtime = kvp.Key;
                var header = kvp.Value.Header;

                if (!header.IsSaved) {
                    var res = await GlobalDialogUtils.ShowDialogAsync(
                        content: $"\"{runtime.FileName}\" {LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Unsave_Intercept_Content))}",
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

                CloseWorkSpaceTab(runtime, kvp.Value.Item);
            }

            return true;
        }

        private void CloseWorkSpaceTab(IRuntime runtime, ArcTabViewItem item) {
            runtime.IsSavedChanged -= Runtime_IsSavedChanged;
            _runtimeToArcTab.Remove(runtime);
            TabViewItems.Remove(item);
        }

        internal IRuntime? GetSelectedRuntime() {
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
