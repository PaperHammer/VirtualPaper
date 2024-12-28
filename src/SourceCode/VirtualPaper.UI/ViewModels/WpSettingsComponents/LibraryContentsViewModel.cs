using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.Models;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UI.Services.Interfaces;
using VirtualPaper.UI.Utils;
using VirtualPaper.UI.ViewModels.AppSettings;
using VirtualPaper.UI.ViewModels.Utils;
using VirtualPaper.UI.Views.Utils;
using VirtualPaper.UIComponent.Container;
using VirtualPaper.UIComponent.Data;
using Windows.Storage;
using Windows.System.UserProfile;
using WinRT.Interop;
using WinUIEx;
using static VirtualPaper.UI.Services.Interfaces.IDialogService;

namespace VirtualPaper.UI.ViewModels.WpSettingsComponents {
    public partial class LibraryContentsViewModel : ObservableObject {
        public ObservableCollection<IWpBasicData> LibraryWallpapers { get; set; }
        public string MenuFlyout_Text_DetailedInfo { get; set; } = string.Empty;
        public string MenuFlyout_Text_Update { get; set; } = string.Empty;
        public string MenuFlyout_Text_EditInfo { get; set; } = string.Empty;
        public string MenuFlyout_Text_Preview { get; set; } = string.Empty;
        public string MenuFlyout_Text_Apply { get; set; } = string.Empty;
        public string MenuFlyout_Text_ApplyToLockBG { get; set; } = string.Empty;
        public string MenuFlyout_Text_ShowOnDisk { get; set; } = string.Empty;
        public string MenuFlyout_Text_Delete { get; set; } = string.Empty;

        public LibraryContentsViewModel(
            IDialogService dialogService,
            IUserSettingsClient userSettingsClient,
            IWallpaperControlClient wallpaperControlClient,
            WpSettingsViewModel wpSettingsViewModel,
            GeneralSettingViewModel generalSettingViewModel) {
            _dialogService = dialogService;
            _userSettingsClient = userSettingsClient;
            _wpControlClient = wallpaperControlClient;

            _wpSettingsViewModel = wpSettingsViewModel;
            generalSettingViewModel.WallpaperInstallDirChanged += WallpaperInstallDirectoryUpdate;

            InitText();
            InitColletions();
        }

        private void InitColletions() {
            LibraryWallpapers = [];
            _wallpaperInstallFolders =
            [
                _userSettingsClient.Settings.WallpaperDir,
            ];
        }

        private void InitText() {
            MenuFlyout_Text_DetailedInfo = App.GetI18n(Constants.I18n.Text_Details);
            MenuFlyout_Text_Update = App.GetI18n(Constants.I18n.Text_UpdateConfig);
            MenuFlyout_Text_EditInfo = App.GetI18n(Constants.I18n.Text_Edit);
            MenuFlyout_Text_Preview = App.GetI18n(Constants.I18n.Text_Preview);
            MenuFlyout_Text_Apply = App.GetI18n(Constants.I18n.Text_Apply);
            MenuFlyout_Text_ApplyToLockBG = App.GetI18n(Constants.I18n.Text_ApplyToLockBG);
            MenuFlyout_Text_ShowOnDisk = App.GetI18n(Constants.I18n.Text_ShowOnDisk);
            MenuFlyout_Text_Delete = App.GetI18n(Constants.I18n.Text_DeleteFromDisk);
        }

        internal async Task InitContentAsync() {
            try {
                BasicUIComponentUtil.Loading(false, false, null);

                var loader = new AsyncLoader<IWpBasicData>(maxDegreeOfParallelism: 10, channelCapacity: 100);
                await foreach (var data in loader.LoadItemsAsync(ProcessFolders, _wallpaperInstallFolders)) {
                    UpdateLib(data);
                }
            }
            catch (Exception ex) {
                BasicUIComponentUtil.ShowExp(ex);
            }
            finally {
                BasicUIComponentUtil.Loaded(null);
            }
        }

        internal async Task DetailedInfoAsync(IWpBasicData data) {
            try {
                if (!data.IsAvailable()) return;
                await CheckFileUpdateAsync(data);

                if (_wp2TocDetail.TryGetValue(data.FilePath, out ToolContainer toolContainer)) {
                    toolContainer.BringToFront();
                    return;
                }

                var mainWindow = App.Services.GetRequiredService<MainWindow>();
                toolContainer = new(
                   mainWindow.WindowStyleType,
                   _userSettingsClient.Settings.ApplicationTheme,
                   mainWindow.WindowCaptionForegroundDisabled,
                   mainWindow.WindowCaptionForeground);

                toolContainer.Closed += ToolContainer_Closed;
                void ToolContainer_Closed(object _, WindowEventArgs __) {
                    toolContainer.Closed -= ToolContainer_Closed;
                    _wp2TocDetail.Remove(data.FilePath);
                }

                SetToolWindowParent(toolContainer, mainWindow);
                AddDetailsPage(data, toolContainer);
                _wp2TocDetail[data.FilePath] = toolContainer;
                toolContainer.Show();
            }
            catch (Exception ex) {
                BasicUIComponentUtil.ShowExp(ex);
            }
        }

        internal async Task EditInfoAsync(IWpBasicData data) {
            try {
                if (!data.IsAvailable()) return;
                await CheckFileUpdateAsync(data);

                if (_wp2TocEdit.TryGetValue(data.FilePath, out ToolContainer toolContainer)) {
                    toolContainer.BringToFront();
                    return;
                }

                var mainWindow = App.Services.GetRequiredService<MainWindow>();
                toolContainer = new(
                   mainWindow.WindowStyleType,
                   _userSettingsClient.Settings.ApplicationTheme,
                   mainWindow.WindowCaptionForegroundDisabled,
                   mainWindow.WindowCaptionForeground);

                toolContainer.Closed += ToolContainer_Closed;
                void ToolContainer_Closed(object _, WindowEventArgs __) {
                    toolContainer.Closed -= ToolContainer_Closed;
                    Edits edits = toolContainer.GetContentByTag($"Edits_{data.FilePath}") as Edits;
                    if (edits.IsSaved) {
                        data.Title = edits.Title;
                        data.Desc = edits.Desc;
                        data.Tags = string.Join(';', edits.TagList);
                        data.Save();
                        UpdateLib(data);
                        BasicUIComponentUtil.ShowMsg(true, Constants.I18n.InfobarMsg_Success, InfoBarSeverity.Success);
                    }

                    _wp2TocEdit.Remove(data.FilePath);
                }

                SetToolWindowParent(toolContainer, mainWindow);
                AddEditsPage(data, toolContainer);
                _wp2TocEdit[data.FilePath] = toolContainer;
                toolContainer.Show();
            }
            catch (Exception ex) {
                BasicUIComponentUtil.ShowExp(ex);
            }
        }

        internal async Task UpdateAsync(IWpBasicData data) {
            try {
                _ctsUpdate = new CancellationTokenSource();
                BasicUIComponentUtil.Loading(true, false, [_ctsUpdate]);

                Grpc_WpBasicData grpc_basicData = await _wpControlClient.UpdateBasicDataAsync(data, _ctsUpdate.Token)
                    ?? throw new Exception("File repair failed.");
                data = DataAssist.GrpcToBasicData(grpc_basicData);
                UpdateLib(data);

                BasicUIComponentUtil.ShowMsg(true, Constants.I18n.InfobarMsg_Success, InfoBarSeverity.Success);
            }
            catch (Exception ex) {
                BasicUIComponentUtil.ShowExp(ex);
            }
            finally {
                BasicUIComponentUtil.Loaded([_ctsUpdate]);
            }
        }

        internal async Task PreviewAsync(IWpBasicData data) {
            try {
                await _previewSemaphoreSlim.WaitAsync();
                if (!data.IsAvailable()) return;
                await CheckFileUpdateAsync(data);

                _ctsPreview = new CancellationTokenSource();
                BasicUIComponentUtil.Loading(true, false, [_ctsPreview]);

                var rtype = await GetWallpaperRTypeByFTypeAsync(data.FType);
                if (rtype == RuntimeType.RUnknown) return;

                await _wpControlClient.PreviewWallpaperAsync(_wpSettingsViewModel.SelectedMonitor.DeviceId, data, rtype, _ctsPreview.Token);
            }
            catch (OperationCanceledException) {
                BasicUIComponentUtil.ShowCanceled();
            }
            catch (Exception ex) {
                BasicUIComponentUtil.ShowExp(ex);
            }
            finally {
                BasicUIComponentUtil.Loaded([_ctsPreview]);
                _previewSemaphoreSlim.Release();
            }
        }

        internal async Task ApplyAsync(IWpBasicData data) {
            try {
                await _applySemaphoreSlim.WaitAsync();
                if (!data.IsAvailable()) return;
                await CheckFileUpdateAsync(data);

                _ctsApply = new CancellationTokenSource();
                BasicUIComponentUtil.Loading(true, false, [_ctsApply]);

                var rtype = await GetWallpaperRTypeByFTypeAsync(data.FType);
                if (rtype == RuntimeType.RUnknown) return;

                Grpc_SetWallpaperResponse response = await _wpControlClient.SetWallpaperAsync(
                    _wpSettingsViewModel.SelectedMonitor,
                    data,
                    rtype,
                    _ctsApply.Token);
                if (!response.IsFinished) {
                    BasicUIComponentUtil.ShowMsg(
                        true,
                        Constants.I18n.Dialog_Content_ApplyError,
                        InfoBarSeverity.Error);
                }
            }
            catch (OperationCanceledException) {
                BasicUIComponentUtil.ShowCanceled();
            }
            catch (Exception ex) {
                BasicUIComponentUtil.ShowExp(ex);
            }
            finally {
                BasicUIComponentUtil.Loaded([_ctsApply]);
                _applySemaphoreSlim.Release();
            }
        }

        internal async Task ApplyToLockBGAsync(IWpBasicData data) {
            try {
                _ctsApplyLockBG = new CancellationTokenSource();
                BasicUIComponentUtil.Loading(true, false, [_ctsApplyLockBG]);
                if (!data.IsAvailable()) return;

                if (data.FType != FileType.FPicture && data.FType != FileType.FGif) {
                    await _dialogService.ShowDialogAsync(
                        App.GetI18n(Constants.I18n.Dialog_Content_OnlyPictureAndGif)
                        , App.GetI18n(Constants.I18n.Dialog_Title_Prompt)
                        , App.GetI18n(Constants.I18n.Text_Confirm));
                    return;
                }

                StorageFile storageFile = await StorageFile.GetFileFromPathAsync(data.FilePath);
                await LockScreen.SetImageFileAsync(storageFile);

                BasicUIComponentUtil.ShowMsg(true, Constants.I18n.InfobarMsg_Success, InfoBarSeverity.Success);
            }
            catch (Exception ex) {
                BasicUIComponentUtil.ShowExp(ex);
            }
            finally {
                BasicUIComponentUtil.Loaded([_ctsApplyLockBG]);
            }
        }

        internal async Task DeleteAsync(IWpBasicData data) {
            try {
                var dialogRes = await _dialogService.ShowDialogAsync(
                    App.GetI18n(Constants.I18n.Dialog_Content_LibraryDelete)
                    , App.GetI18n(Constants.I18n.Dialog_Title_Prompt)
                    , App.GetI18n(Constants.I18n.Text_Confirm)
                    , App.GetI18n(Constants.I18n.Text_Cancel));
                if (dialogRes != DialogResult.Primary) return;

                bool isUsing = await CheckFileUsingAsync(data, false);
                if (isUsing) return;

                string uid = data.WallpaperUid;
                _uid2idx.Remove(uid, out _);
                LibraryWallpapers.Remove(data);
                DirectoryInfo di = new(data.FolderPath);
                di.Delete(true);
            }
            catch (Exception ex) {
                BasicUIComponentUtil.ShowExp(ex);
            }
        }

        private void UpdateLib(IWpBasicData data) {
            try {
                ArgumentNullException.ThrowIfNull(nameof(data));
                if (_uid2idx.TryGetValue(data.WallpaperUid, out int idx)) {
                    LibraryWallpapers[idx] = data;
                }
                else {
                    _uid2idx[data.WallpaperUid] = LibraryWallpapers.Count;
                    LibraryWallpapers.Add(data);
                }
            }
            catch (Exception ex) {
                BasicUIComponentUtil.ShowExp(ex);
            }
        }

        internal async Task DropFilesAsync(IReadOnlyList<IStorageItem> items) {
            try {
                _ctsImport = new CancellationTokenSource();
                BasicUIComponentUtil.Loading(true, true, [_ctsImport]);
                List<ImportValue> importValues = await GetImportValueFromLocalAsync(items);
                await ImportFromValuesAsync(importValues);
            }
            finally {
                BasicUIComponentUtil.Loaded([_ctsImport]);
            }
        }

        private async Task ImportFromValuesAsync(List<ImportValue> importValues) {
            try {
                int finishedCnt = 0;
                BasicUIComponentUtil.UpdateProgressbarValue(0, importValues.Count);

                foreach (var importValue in importValues) {
                    try {
                        if (importValue.FType != FileType.FUnknown) {
                            var grpc_data = await _wpControlClient.CreateBasicDataAsync(
                                importValue.FilePath,
                                importValue.FType,
                                _ctsImport.Token);
                            IWpBasicData data = DataAssist.GrpcToBasicData(grpc_data);
                            UpdateLib(data);
                        }
                        else {
                            await _dialogService.ShowDialogAsync(
                               $"\"{importValue.FilePath}\"\n" + App.GetI18n(Constants.I18n.Dialog_Content_Import_Failed_Lib)
                               , App.GetI18n(Constants.I18n.Dialog_Title_Prompt)
                               , App.GetI18n(Constants.I18n.Text_Confirm));
                        }

                        BasicUIComponentUtil.UpdateProgressbarValue(++finishedCnt, importValues.Count);
                    }
                    catch (Exception ex) {
                        BasicUIComponentUtil.ShowExp(ex);
                    }

                    if (_ctsImport.IsCancellationRequested)
                        throw new OperationCanceledException();
                }
            }
            catch (OperationCanceledException) {
                BasicUIComponentUtil.ShowCanceled();
            }
        }

        private async Task<RuntimeType> GetWallpaperRTypeByFTypeAsync(FileType ftype) {
            switch (ftype) {
                case FileType.FPicture:
                case FileType.FGif:
                    var wpCreateDialogViewModel = new WallpaperCreateDialogViewModel();
                    var dialogRes = await _dialogService.ShowDialogAsync(
                        new WallpaperCreateView(wpCreateDialogViewModel)
                        , App.GetI18n(Constants.I18n.Dialog_Title_CreateType)
                        , App.GetI18n(Constants.I18n.Text_Confirm)
                        , App.GetI18n(Constants.I18n.Text_Cancel));
                    if (dialogRes != DialogResult.Primary) return RuntimeType.RUnknown;

                    return wpCreateDialogViewModel.SelectedItem.CreateType switch {
                        WallpaperCreateType.Img => RuntimeType.RImage,
                        WallpaperCreateType.DepthImg => RuntimeType.RImage3D,
                        _ => RuntimeType.RUnknown,
                    };
                case FileType.FVideo:
                    return RuntimeType.RVideo;
                default:
                    return RuntimeType.RUnknown;
            }
        }

        private async void WallpaperInstallDirectoryUpdate(object sender, EventArgs e) {
            LibraryWallpapers.Clear();
            _wallpaperInstallFolders.Clear();
            _wallpaperInstallFolders.Add(_userSettingsClient.Settings.WallpaperDir);

            var loader = new AsyncLoader<IWpBasicData>(maxDegreeOfParallelism: 10, channelCapacity: 100);
            await foreach (var data in loader.LoadItemsAsync(ProcessFolders, _wallpaperInstallFolders)) {
                LibraryWallpapers.Add(data);
            }
        }

        private async Task CheckFileUpdateAsync(IWpBasicData data) {
            if (data.AppInfo.FileVersion != _userSettingsClient.Settings.FileVersion) {
                var dialogRes = await _dialogService.ShowDialogAsync(
                    App.GetI18n(Constants.I18n.Dialog_Content_Import_NeedUpdate)
                    , App.GetI18n(Constants.I18n.Dialog_Title_Prompt)
                    , App.GetI18n(Constants.I18n.Text_Confirm)
                    , App.GetI18n(Constants.I18n.Text_Cancel));
                if (dialogRes == DialogResult.Primary)
                    await UpdateAsync(data);
            }
        }

        private async Task<bool> CheckFileUsingAsync(IWpBasicData data, bool isSlient) {
            await _userSettingsClient.LoadAsync<List<IWallpaperLayout>>();
            foreach (var wl in _userSettingsClient.WallpaperLayouts) {
                if (wl.FolderPath == data.FolderPath) {
                    if (!isSlient) {
                        await _dialogService.ShowDialogAsync(
                        App.GetI18n(Constants.I18n.Dialog_Content_WpIsUsing)
                        , App.GetI18n(Constants.I18n.Dialog_Title_Prompt)
                        , App.GetI18n(Constants.I18n.Text_Confirm));
                    }

                    return true;
                }
            }

            return false;
        }

        private static void AddDetailsPage(IWpBasicData data, ToolContainer toolContainer) {
            Details details = new(data);
            toolContainer.AddContent(Constants.I18n.Text_Details, $"Details_{data.FilePath}", details);
        }

        private static void AddEditsPage(IWpBasicData data, ToolContainer toolContainer) {
            Edits edits = new(data, toolContainer);
            toolContainer.AddContent(Constants.I18n.Text_Edit, $"Edits_{data.FilePath}", edits);
        }

        private static void SetToolWindowParent(WindowEx childWindow, WindowEx parentWindow) {
            IntPtr toolHwnd = WindowNative.GetWindowHandle(childWindow);
            Native.SetWindowLong(toolHwnd, Native.GWL_HWNDPARENT, WindowNative.GetWindowHandle(parentWindow));
        }

        private static async Task ProcessFolders(IEnumerable<string> folderPaths, ParallelOptions parallelOptions, ChannelWriter<IWpBasicData> writer, CancellationToken cancellationToken = default) {
            await Parallel.ForEachAsync(folderPaths, parallelOptions, async (storeDir, token) => {
                DirectoryInfo root = new(storeDir);
                DirectoryInfo[] folders = root.GetDirectories();

                foreach (DirectoryInfo folder in folders) {
                    string[] files = Directory.GetFiles(folder.FullName);

                    foreach (string file in files) {
                        if (Path.GetFileName(file) == Constants.Field.WpBasicDataFileName) {
                            WpBasicData data = await JsonStorage<WpBasicData>.LoadDataAsync(file);

                            if (data.IsAvailable()) {
                                await writer.WriteAsync(data, token);
                                break;
                            }
                        }
                        token.ThrowIfCancellationRequested();
                    }
                    token.ThrowIfCancellationRequested();
                }
            });
        }

        private static async Task<List<ImportValue>> GetImportValueFromLocalAsync(IReadOnlyList<IStorageItem> items) {
            ConcurrentBag<ImportValue> importRes = [];
            SemaphoreSlim semaphore = new(20); // 并发度控制

            var tasks = items.Select(async item => {
                await semaphore.WaitAsync();

                try {
                    if (item is StorageFile file) {
                        importRes.Add(new(file.Path, FileFilter.GetFileType(file.Path)));
                    }
                    else if (item is StorageFolder folder) {
                        var subItems = await folder.GetItemsAsync();
                        var subResults = await GetImportValueFromLocalAsync(subItems);
                        foreach (var res in subResults) {
                            importRes.Add(res);
                        }
                    }
                }
                finally {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            return [.. importRes];
        }

        private struct ImportValue(string filePath, FileType ftype) {
            internal string FilePath { get; set; } = filePath;
            internal FileType FType { get; set; } = ftype;
        }

        private readonly IDialogService _dialogService;
        private readonly IWallpaperControlClient _wpControlClient;
        private readonly IUserSettingsClient _userSettingsClient;
        private readonly WpSettingsViewModel _wpSettingsViewModel;
        private List<string> _wallpaperInstallFolders;
        private CancellationTokenSource _ctsImport, _ctsUpdate, _ctsApply, _ctsApplyLockBG, _ctsPreview;
        private readonly ConcurrentDictionary<string, int> _uid2idx = [];
        private readonly SemaphoreSlim _applySemaphoreSlim = new(1, 1);
        private readonly SemaphoreSlim _previewSemaphoreSlim = new(1, 1);
        private static readonly Dictionary<string, ToolContainer> _wp2TocDetail = [];
        private static readonly Dictionary<string, ToolContainer> _wp2TocEdit = [];
    }
}
