using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
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
using VirtualPaper.UIComponent.Utils;
using Windows.Storage;
using Windows.System.UserProfile;
using WinRT.Interop;
using WinUI3Localizer;
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
        public string MenuFlyout_Text_Export { get; set; } = string.Empty;
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
            _localizer = LanguageUtil.LocalizerInstacne;

            _wpSettingsViewModel = wpSettingsViewModel;
            generalSettingViewModel.WallpaperInstallDirChanged += WallpaperInstallDirectoryUpdate;

            InitText();
            InitColletions();
            InitContents();
        }

        private void InitColletions() {
            LibraryWallpapers = [];

            _wallpaperInstallFolders =
            [
                _userSettingsClient.Settings.WallpaperDir,
            ];
        }

        private void InitText() {
            MenuFlyout_Text_DetailedInfo = _localizer.GetLocalizedString(Constants.LocalText.MenuFlyout_Text_DetailedInfo);
            MenuFlyout_Text_Update = _localizer.GetLocalizedString(Constants.LocalText.MenuFlyout_Text_Update);
            MenuFlyout_Text_EditInfo = _localizer.GetLocalizedString(Constants.LocalText.MenuFlyout_Text_EditInfo);
            MenuFlyout_Text_Preview = _localizer.GetLocalizedString(Constants.LocalText.MenuFlyout_Text_Preview);
            MenuFlyout_Text_Apply = _localizer.GetLocalizedString(Constants.LocalText.MenuFlyout_Text_Apply);
            MenuFlyout_Text_ApplyToLockBG = _localizer.GetLocalizedString(Constants.LocalText.MenuFlyout_Text_ApplyToLockBG);
            MenuFlyout_Text_ShowOnDisk = _localizer.GetLocalizedString(Constants.LocalText.MenuFlyout_Text_ShowOnDisk);
            MenuFlyout_Text_Export = _localizer.GetLocalizedString(Constants.LocalText.MenuFlyout_Text_Export);
            MenuFlyout_Text_Delete = _localizer.GetLocalizedString(Constants.LocalText.MenuFlyout_Text_Delete);
        }

        internal async void InitContents() {
            try {
                BasicUIComponentUtil.Loading(false, false, null);
                LibraryWallpapers.Clear();

                await foreach (var libData in GetWpBasicDataByInstallFoldersAsync(_wallpaperInstallFolders)) {
                    _uid2idx[libData.BasicData.WallpaperUid] = libData.Idx;
                    LibraryWallpapers.Add(libData.BasicData);
                }
            }
            catch (Exception ex) {
                BasicUIComponentUtil.ShowExp(ex);
                LibraryWallpapers.Clear();
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
                   mainWindow.AppWindow.TitleBar.ButtonForegroundColor,
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
                   mainWindow.AppWindow.TitleBar.ButtonForegroundColor,
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
                        BasicUIComponentUtil.ShowMsg(true, "Msg_Save_Succeded", InfoBarSeverity.Success);
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

                BasicUIComponentUtil.ShowMsg(true, Constants.LocalText.InfobarMsg_Success, InfoBarSeverity.Success);
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

                await _wpControlClient.PreviewWallpaperAsync(data, rtype, _ctsPreview.Token);
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
                    _wpSettingsViewModel.Monitors[_wpSettingsViewModel.MonitorSelectedIdx],
                    data,
                    rtype,
                    _ctsApply.Token);
                if (!response.IsFinished) {
                    BasicUIComponentUtil.ShowMsg(
                        true,
                        Constants.LocalText.Dialog_Content_ApplyError,
                        InfoBarSeverity.Error);
                }
                else {
                    _wpSettingsViewModel.Monitors[_wpSettingsViewModel.MonitorSelectedIdx].ThumbnailPath = data.ThumbnailPath;
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
                        _localizer.GetLocalizedString(Constants.LocalText.Dialog_Content_OnlyPictureAndGif)
                        , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Title_Prompt)
                        , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm));
                    return;
                }

                StorageFile storageFile = await StorageFile.GetFileFromPathAsync(data.FilePath);
                await LockScreen.SetImageFileAsync(storageFile);

                BasicUIComponentUtil.ShowMsg(true, Constants.LocalText.InfobarMsg_Success, InfoBarSeverity.Success);
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
                    _localizer.GetLocalizedString(Constants.LocalText.Dialog_Content_LibraryDelete)
                    , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Title_Prompt)
                    , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm)
                    , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Cancel));
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
                lock (_lockAddToLib) {
                    if (_uid2idx.TryGetValue(data.WallpaperUid, out int idx)) {
                        LibraryWallpapers[idx] = data;
                    }
                    else {
                        _uid2idx[data.WallpaperUid] = LibraryWallpapers.Count;
                        LibraryWallpapers.Add(data);
                    }
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
                            IWpBasicData data = await SetBasicDataAsync(importValue, _ctsImport);

                            if (_ctsImport.IsCancellationRequested)
                                throw new OperationCanceledException();

                            UpdateLib(data);
                        }
                        else {
                            await _dialogService.ShowDialogAsync(
                               $"\"{importValue.FilePath}\"\n" + _localizer.GetLocalizedString(Constants.LocalText.Dialog_Content_Import_Failed_Lib)
                               , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Title_Prompt)
                               , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm));
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

        private async Task<IWpBasicData> SetBasicDataAsync(ImportValue importValue, CancellationTokenSource token) {
            var wpMetadataBasic = await _wpControlClient.CreateBasicDataAsync(
                importValue.FilePath,
                importValue.FType,
                token.Token);

            return DataAssist.GrpcToBasicData(wpMetadataBasic);
        }

        //private async Task<bool> SetRuntimeDataAsync(IWpMetadata data, CancellationTokenSource token) {
        //    var rtype = await GetWallpaperRTypeByFTypeAsync(data.BasicData.FType);
        //    if (rtype == RuntimeType.RUnknown) return false;

        //    var wpMetadataRuntime = await _wpControlClient.CreateRuntimeDataAsync(
        //        data.BasicData.FilePath,
        //        data.BasicData.FolderPath,
        //        rtype,
        //        token.Token);
        //    data.RuntimeData = DataAssist.GrpcToRuntimeData(wpMetadataRuntime);

        //    return true;
        //}

        private async Task<RuntimeType> GetWallpaperRTypeByFTypeAsync(FileType ftype) {
            switch (ftype) {
                case FileType.FPicture:
                case FileType.FGif:
                    var wpCreateDialogViewModel = new WallpaperCreateDialogViewModel();
                    var dialogRes = await _dialogService.ShowDialogAsync(
                        new WallpaperCreateView(wpCreateDialogViewModel)
                        , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Title_CreateType)
                        , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm));
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

            await foreach (var libData in GetWpBasicDataByInstallFoldersAsync(_wallpaperInstallFolders)) {
                var md = libData.BasicData;
                LibraryWallpapers.Add(md);
            }
        }

        private async Task CheckFileUpdateAsync(IWpBasicData data) {
            if (data.AppInfo.FileVersion != _userSettingsClient.Settings.FileVersion) {
                var dialogRes = await _dialogService.ShowDialogAsync(
                    _localizer.GetLocalizedString(Constants.LocalText.Dialog_Content_Import_NeedUpdate)
                    , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Title_Prompt)
                    , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm)
                    , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Cancel));
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
                        _localizer.GetLocalizedString(Constants.LocalText.Dialog_Content_WpIsUsing)
                        , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Title_Prompt)
                        , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm));
                    }

                    return true;
                }
            }

            return false;
        }

        private static void AddDetailsPage(IWpBasicData data, ToolContainer toolContainer) {
            Details details = new(data);
            toolContainer.AddContent(Constants.LocalText.WpConfigViewMdoel_TextDetailedInfo, $"Details_{data.FilePath}", details);
        }

        private static void AddEditsPage(IWpBasicData data, ToolContainer toolContainer) {
            Edits edits = new(data, toolContainer);
            toolContainer.AddContent(Constants.LocalText.Wp_TextEditInfo, $"Edits_{data.FilePath}", edits);
        }

        private static void SetToolWindowParent(WindowEx childWindow, WindowEx parentWindow) {
            IntPtr toolHwnd = WindowNative.GetWindowHandle(childWindow);
            Native.SetWindowLong(toolHwnd, Native.GWL_HWNDPARENT, WindowNative.GetWindowHandle(parentWindow));
        }

        private static async IAsyncEnumerable<WpLibData> GetWpBasicDataByInstallFoldersAsync(List<string> folderPaths) {
            int idx = 0;
            foreach (string storeDir in folderPaths) {
                DirectoryInfo root = new(storeDir);
                DirectoryInfo[] folders = root.GetDirectories();

                foreach (DirectoryInfo folder in folders) {
                    string[] files = Directory.GetFiles(folder.FullName);
                    WpLibData libData = new();
                    foreach (string file in files) {
                        if (Path.GetFileName(file) == Constants.Field.WpBasicDataFileName) {
                            libData.BasicData = await JsonStorage<WpBasicData>.LoadDataAsync(file);

                            if (libData.BasicData.IsAvailable()) {
                                libData.Idx = idx++;
                                yield return libData;
                                break;
                            }
                        }
                    }
                }
            }
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

        private readonly ILocalizer _localizer;
        private readonly IDialogService _dialogService;
        private readonly IWallpaperControlClient _wpControlClient;
        private readonly IUserSettingsClient _userSettingsClient;
        private readonly WpSettingsViewModel _wpSettingsViewModel;
        private List<string> _wallpaperInstallFolders;
        private CancellationTokenSource _ctsImport, _ctsUpdate, _ctsApply, _ctsApplyLockBG, _ctsPreview;
        private readonly ConcurrentDictionary<string, int> _uid2idx = [];
        private readonly static object _lockAddToLib = new(), _lockCollection = new();
        private readonly SemaphoreSlim _applySemaphoreSlim = new(1, 1);
        private readonly SemaphoreSlim _previewSemaphoreSlim = new(1, 1);
        private static readonly Dictionary<string, ToolContainer> _wp2TocDetail = [];
        private static readonly Dictionary<string, ToolContainer> _wp2TocEdit = [];
    }
}
