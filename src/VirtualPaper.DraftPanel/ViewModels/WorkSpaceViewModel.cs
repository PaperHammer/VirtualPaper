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
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Common.Utils.UndoRedo.Events;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.DraftPanel.Services;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Navigation;
using VirtualPaper.UIComponent.Navigation.TabView;
using VirtualPaper.UIComponent.Navigation.TabView.Interfaces;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.Adapter.Interfaces;
using Windows.Storage;
using Workloads.Entry;
using Workloads.Entry.FileLoaders;
using Workloads.Entry.FileLoaders.Specific;
using Workloads.Entry.Interfaces;
using Workloads.Utils.DraftUtils.Interfaces;
using Workloads.Utils.DraftUtils.Models;

namespace VirtualPaper.DraftPanel.ViewModels {
    public partial class WorkSpaceViewModel : ObservableObject, IDisposable {
        public ObservableCollection<IArcTabViewItem> TabViewItems { get; set; } = [];

        int _selectedTabIndex = -1;
        public int SelectedTabIndex {
            get { return _selectedTabIndex; }
            set { if (_selectedTabIndex == value) return; _selectedTabIndex = value; OnPropertyChanged(); }
        }

        public ICommand? MFI_OpenCommand { get; private set; }
        public ICommand? MFI_SaveCommand { get; private set; }
        public ICommand? MFI_SaveAsCommand { get; private set; }
        public ICommand? MFI_SaveAllCommand { get; private set; }
        public ICommand? MFI_ExitCommand { get; private set; }
        public ICommand? MFI_UndoCommand { get; private set; }
        public ICommand? MFI_RedoCommand { get; private set; }
        public ICommand? MFI_ManualCommand { get; private set; }
        public ICommand? MFI_AboutCommand { get; private set; }

        public WorkSpaceViewModel(
            IUserSettingsClient userSettings,
            IRuntimeFactory runtimeFactory,
            IWorkspaceSaveCoordinator saveCoordinator,
            ProjectFileLoaderRegistry fileLoaderRegistry) {
            this._userSettings = userSettings;
            this._runtimeFactory = runtimeFactory;
            this._saveCoordinator = saveCoordinator;
            this._fileLoaderRegistry = fileLoaderRegistry;
            InitCommand();
        }

        private void InitCommand() {
            MFI_OpenCommand = new RelayCommand(async () => {
                await OpenAsync();
            });
            MFI_SaveCommand = new RelayCommand(async () => {
                await SaveAsync();
            });
            MFI_SaveAsCommand = new RelayCommand(async () => {
                await SaveAsAsync();
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

        public void OnTabItemsChanged(TabView sender, Windows.Foundation.Collections.IVectorChangedEventArgs args) {
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
        private async Task OpenAsync() {
            var storage = await WindowsStoragePickers.PickFilesAsync(
                 WindowConsts.WindowHandle,
                 [.. FileFilter.FileTypeToExtension[FileType.FDesign], .. FileFilter.FileTypeToExtension[FileType.FWebDesign], .. FileFilter.FileTypeToExtension[FileType.FImage]],
                 true);
            await OpenLocalFilesAsync(storage);
        }

        private async Task OpenLocalFilesAsync(StorageFile[]? items) {
            if (items == null || items.Length < 1) return;

            PreProjectData[] datas = items
                .Select(item => new PreProjectData(item.Path, ProjectType.P_StaticImage, ProjectOpenIntent.OpenExisting))
                .ToArray();
            await AddNewItemsAsync(datas);
        }

        internal async Task ExportAsync(ExportImageFormat format) => await ExecuteRuntimeCommandAsync(x => x.ExportAsync(format));
        internal async Task<bool> AddToLibraryAsync() => await ExecuteRuntimeCommandAsync(x => x.AddToLibraryAsync());
        private async Task SaveAsync() => await ExecuteRuntimeCommandAsync(InternalSaveAsync);
        private async Task SaveAsAsync() => await ExecuteRuntimeCommandAsync(InternalSaveAsAsync);
        /*
         * 原实现 Task.WhenAll 会并发保存全部运行时；
         * 改串行是为避免多个运行时同时写文件、更新共享配置、触发 watcher 或占用同一底层资源，导致 I/O 峰值、事件交错、失败状态难以定位。
         */
        private async Task SaveAllAsync() {
            var runtimes = TabViewItems
                .Select(item => item.Tag as IRuntime)
                .OfType<IRuntime>()
                .ToList();

            foreach (var runtime in runtimes) {
                await InternalSaveAsync(runtime);
            }
        }

        private async Task UndoAsync() => await ExecuteRuntimeCommandAsync(x => x.UndoAsync());
        private async Task RedoAsync() => await ExecuteRuntimeCommandAsync(x => x.RedoAsync());

        private Task ExecuteRuntimeCommandAsync(Func<IRuntime, Task> command, IArcTabViewItem? specificItem = null) {
            var runtime = (specificItem?.Tag as IRuntime) ?? GetSelectedRuntime();
            return runtime != null
                ? command(runtime)
                : Task.CompletedTask;
        }

        private async Task<T?> ExecuteRuntimeCommandAsync<T>(Func<IRuntime, Task<T>> command, IArcTabViewItem? specificItem = null) {
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

        private void RefreshHeaderAsync(IRuntime runtime) {
            if (!_runtimeToArcTab.TryGetValue(runtime, out var tab)) return;

            CrossThreadInvoker.InvokeOnUIThread(() => {
                if (tab.Header.MainContent is TextBlock tb) {
                    tb.Text = Path.GetFileName(runtime.FileName);
                }
            });
        }
        #endregion

        #region project
        private async Task InternalSaveAsync(IRuntime runtime) {
            await runtime.SaveAsync();
            RefreshHeaderAsync(runtime);
        }

        private async Task InternalSaveAsAsync(IRuntime runtime) {
            await runtime.SaveAsAsync();
            RefreshHeaderAsync(runtime);
        }

        internal async Task<bool> AddNewItemsAsync(PreProjectData[]? predatas) {
            if (predatas == null || predatas.Length == 0) return false;

            var hasAddedItem = false;
            foreach (var data in predatas) {
                try {
                    hasAddedItem |= await AddNewItemAsync(data);
                }
                catch (Exception ex) {
                    ArcLog.GetLogger<WorkSpaceViewModel>().Error($"Failed to process project item: {data.Identity}", ex);
                    GlobalMessageUtil.ShowException(ex);
                }
            }

            if (hasAddedItem && !_tempRecentUsed.IsEmpty) {
                await _userSettings.UpdateRecetUsedAsync(_tempRecentUsed.ToArray());
            }

            return hasAddedItem;
        }

        private async Task<bool> AddNewItemAsync(PreProjectData data) {
            return data.Intent switch {
                ProjectOpenIntent.OpenExisting => await InitRuntimeWithFileAsync(data.Identity),
                ProjectOpenIntent.CreateFromName or ProjectOpenIntent.CreateAtDirectory => InitRuntimeWithIdentify(data.Identity, data.Type),
                _ => throw new ArgumentOutOfRangeException(nameof(data), $"Unsupported project open intent: {data.Intent}"),
            };
        }

        private async Task<bool> InitRuntimeWithFileAsync(string filePath) {
            if (!FileUtil.IsValidFilePath(filePath)) {
                GlobalMessageUtil.ShowError(
                    message: nameof(Constants.I18n.Project_FileLoad_Failed),
                    isNeedLocalizer: true,
                    extraMsg: filePath);
                return false;
            }

            var result = await _fileLoaderRegistry.LoadAsync(filePath);
            if (result == null) return false;

            AddToWorkSpace(result.FilePath, result.FileType);
            AddRecentUsed(filePath);
            return true;
        }

        private bool InitRuntimeWithIdentify(string identity, ProjectType type) {
            var fileType = type switch {
                ProjectType.P_StaticImage => FileType.FDesign,
                ProjectType.P_WebBackdrop => FileType.FWebDesign,
                _ => (FileType?)null,
            };
            if (fileType == null) return false;

            AddToWorkSpace(identity, fileType.Value);

            if (fileType == FileType.FWebDesign) {
                AddRecentUsed(GetWebProjectFilePath(identity));
            }

            return true;
        }

        private void AddRecentUsed(string filePath) {
            if (!_tempRecentUsed.Contains(filePath)) {
                _tempRecentUsed.Add(filePath);
            }
        }

        private static string GetWebProjectFilePath(string projectFolder) {
            var normalizedFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var projectName = Path.GetFileName(normalizedFolder);
            return Path.Combine(normalizedFolder, projectName + FileExtension.FE_WebDesign);
        }

        private void AddToWorkSpace(string file, FileType fileType) {
            CrossThreadInvoker.InvokeOnUIThread(() => {
                var runtime = _runtimeFactory.Create(file, fileType);
                runtime.IsSavedChanged += Runtime_IsSavedChanged;

                var header = new ArcTabViewItemHeader() {
                    MainContent = new TextBlock {
                        Text = Path.GetFileName(file),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = 200
                    },
                    IsSaved = runtime.IsSavedFromInit,
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
                value.Header.IsSaved = e.IsSaved && runtime.IsSavedFromInit;
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
                    ClearCommand();
                }
                _isDisposed = true;
            }
        }

        private void ClearCommand() {
            MFI_OpenCommand = null;
            MFI_SaveCommand = null;
            MFI_SaveAsCommand = null;
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

        public async IAsyncEnumerable<IArcTabViewItem> HandleExitItemsAsync() {
            var tabsToClose = TabViewItems.ToList();

            foreach (var tabItem in tabsToClose) {
                if (tabItem.Tag is not IRuntime runtime) continue;
                if (!_runtimeToArcTab.TryGetValue(runtime, out var value)) continue;
                if (value.Header.IsSaved) continue;
                if (!await _saveCoordinator.CanCloseAsync(runtime, value.Header.IsSaved, canCancel: false)) continue;

                CloseWorkSpaceTab(runtime, tabItem);
                yield return tabItem;
            }
        }

        public async Task<bool> CheckSaveStatusAsync(IRuntime runtime) {
            if (!_runtimeToArcTab.TryGetValue(runtime, out var value)) return false;
            if (!await _saveCoordinator.CanCloseAsync(runtime, value.Header.IsSaved, canCancel: true)) return false;

            CloseWorkSpaceTab(runtime, value.Item);
            return true;
        }

        public async Task<bool> CheckAllSaveStatusAsync() {
            foreach (var kvp in _runtimeToArcTab.ToList()) {
                var runtime = kvp.Key;
                var tab = kvp.Value;

                if (!await _saveCoordinator.CanCloseAsync(runtime, tab.Header.IsSaved, canCancel: true)) {
                    return false;
                }

                CloseWorkSpaceTab(runtime, tab.Item);
            }

            return true;
        }

        private void CloseWorkSpaceTab(IRuntime runtime, IArcTabViewItem item) {
            runtime.IsSavedChanged -= Runtime_IsSavedChanged;
            _runtimeToArcTab.Remove(runtime);
            TabViewItems.Remove(item);
        }

        public IRuntime? GetSelectedRuntime() {
            if (SelectedTabIndex < 0 || SelectedTabIndex >= TabViewItems.Count) return null;
            return TabViewItems[SelectedTabIndex].Tag as IRuntime;
        }
        #endregion

        internal readonly ObservableCollection<MenuBarItem> _middleMenuItems = [];
        private readonly IUserSettingsClient _userSettings;
        private readonly IRuntimeFactory _runtimeFactory;
        private readonly IWorkspaceSaveCoordinator _saveCoordinator;
        private readonly ProjectFileLoaderRegistry _fileLoaderRegistry;
        private readonly ConcurrentBag<string> _tempRecentUsed = [];
        private readonly Dictionary<IRuntime, (IArcTabViewItemHeader Header, IArcTabViewItem Item)> _runtimeToArcTab = [];
    }
}
