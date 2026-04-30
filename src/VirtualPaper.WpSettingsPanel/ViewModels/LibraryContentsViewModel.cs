using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.PlayerWeb.Core.WebView.Windows;
using VirtualPaper.UIComponent.Others;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.ViewModels;
using VirtualPaper.WpSettingsPanel.Utils;
using Windows.Storage;
using Windows.System.UserProfile;
using WinUIEx;

namespace VirtualPaper.WpSettingsPanel.ViewModels {
    public partial class LibraryContentsViewModel : ObservableObject, IFilterable {
        public ObservableCollection<IWpBasicData> LibraryWallpapers { get; private set; } = null!;

        //private Brush _wpTitleForeground = new SolidColorBrush(Colors.White);
        //public Brush WpTitleForeground {
        //    get { return _wpTitleForeground; }
        //    set { _wpTitleForeground = value; OnPropertyChanged(); }
        //}
        private byte[] _wpTitleForeground = [255, 255, 255, 255];
        public byte[] WpTitleForeground {
            get => _wpTitleForeground;
            set { _wpTitleForeground = value; OnPropertyChanged(); }
        }

        private LoadingStatus _libLoadingStatus;
        public LoadingStatus LibLoadingStatus {
            get { return _libLoadingStatus; }
            set { _libLoadingStatus = value; OnPropertyChanged(); }
        }

        public LibraryContentsViewModel(
            IUserSettingsClient userSettingsClient,
            IWallpaperControlClient wallpaperControlClient,
            WpSettingsViewModel wpSettingsViewModel,
            WallpaperIndexService wallpaperIndexService) {
            _userSettingsClient = userSettingsClient;
            _wpControlClient = wallpaperControlClient;
            _wpSettingsViewModel = wpSettingsViewModel;
            _wallpaperIndexService = wallpaperIndexService;

            InitEvent();
            InitColletions();
            InitOthers();
        }

        private void InitOthers() {
            _wallpaperIndexService.Initialize(_wallpaperInstallFolders);
            _wpSettingsViewModel.RegisterLibraryContents(this);
        }

        private void InitEvent() {
            ArcThemeUtil.AppThemeChanged += (s, e) => {
                RefreshWpTitleForeground();
            };
        }

        internal void RefreshWpTitleForeground() {
            WpTitleForeground = ArcThemeUtil.GetFormatMainWindowTheme() == AppTheme.Light
                ? [255, 255, 255, 255]   // White: A=255, R=255, G=255, B=255
                : [255, 0, 0, 0];        // Black: A=255, R=0, G=0, B=0
        }

        private void InitColletions() {
            _wallpaperInstallFolders = [
                _userSettingsClient.Settings.WallpaperDir,
            ];
            LibraryWallpapers = [];
            _libraryWallpapers = [];
        }

        internal async Task InitContentAsync() {
            if (_isInited) return;

            var ctx = ArcPageContextManager.GetContext<WpSettings>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            await loadingCtx.RunAsync(
                operation: async token => {
                    try {
                        await _wallpaperIndexService.Initialized.Task;

                        var entries = _wallpaperIndexService.Query(_offset, _limit);
                        foreach (var entry in entries) {
                            var jsonPath = entry.JsonPath;
                            WpBasicData? data = await JsonSaver.LoadAsync<WpBasicData>(jsonPath, WpBasicDataContext.Default);
                            if (data == null || !data.IsAvailable())
                                continue;

                            LibraryWallpapers.Add(data);
                            _libraryWallpapers.Add(data);
                            _offset++;
                        }

                        _isInited = true;
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                        GlobalMessageUtil.ShowException(ex);
                    }
                });
        }

        internal void ShowDetail(IWpBasicData data) {
            try {
                if (!data.IsAvailable()) return;

                if (_details.TryGetValue(data.WallpaperUid, out var onlyDetail)) {
                    onlyDetail.Activate();
                    return;
                }

                var onlyDetailWindow = data.FType switch {
                    FileType.FImage or FileType.FGif or FileType.FVideo => new OnlyDetails(DataConfigTab.GeneralInfo, data),
                    _ => throw new NotImplementedException(),
                };
                onlyDetailWindow.Closed += (sender, args) => {
                    _details.Remove(data.WallpaperUid);
                };
                _details.Add(data.WallpaperUid, onlyDetailWindow);
                onlyDetailWindow.Show();
                onlyDetailWindow.Activate();
            }
            catch (Exception ex) {
                ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
            }
        }

        internal void ShowEdit(IWpBasicData data) {
            try {
                if (!data.IsAvailable()) return;

                if (_edits.TryGetValue(data.WallpaperUid, out var editDetail)) {
                    editDetail.Activate();
                    return;
                }

                var editDetailWindow = data.FType switch {
                    FileType.FImage or FileType.FGif or FileType.FVideo => new OnlyDetails(DataConfigTab.GeneralInfoEdit, data),
                    _ => throw new NotImplementedException(),
                };
                editDetailWindow.Closed += (sender, args) => {
                    _edits.Remove(data.WallpaperUid);
                };
                _edits.Add(data.WallpaperUid, editDetailWindow);
                editDetailWindow.Show();
                editDetailWindow.Activate();
            }
            catch (Exception ex) {
                ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
            }
        }

        internal async Task UpdateAsync(IWpBasicData data) {
            var ctx = ArcPageContextManager.GetContext<WpSettings>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            await loadingCtx.RunAsync(
                operation: async token => {
                    try {
                        bool isUsing = await IsFileInUseAsync(data);
                        if (isUsing) {
                            GlobalMessageUtil.ShowInfo(message: Constants.I18n.Text_FileUsing, isNeedLocalizer: true);
                            return;
                        }

                        bool isPreview = IsFileInPreview(data);
                        if (isPreview) {
                            GlobalMessageUtil.ShowInfo(message: Constants.I18n.Text_FileInPreview, isNeedLocalizer: true);
                            return;
                        }

                        Grpc_WpBasicData grpc_basicData = await _wpControlClient.UpdateBasicDataAsync(data.FolderPath, data.FolderName, data.FilePath, data.FType)
                            ?? throw new Exception("Config update failed.");
                        data = DataAssist.GrpcToBasicData(grpc_basicData);
                        UpdateLib(data);

                        GlobalMessageUtil.ShowSuccess(message: Constants.I18n.InfobarMsg_Success, isNeedLocalizer: true);
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                        GlobalMessageUtil.ShowException(ex);
                    }
                });
        }

        internal async Task PreviewAsync(IWpBasicData data) {
            var ctx = ArcPageContextManager.GetContext<WpSettings>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            var ctsPreview = new CancellationTokenSource();
            await loadingCtx.RunAsync(
                operation: async token => {
                    try {
                        if (!data.IsAvailable()) return;
                        await CheckFileUpdateAsync(data);

                        var rtype = await GetWallpaperRTypeByFTypeAsync(data.FType);
                        if (rtype == RuntimeType.RUnknown) return;

                        if (_previews.TryGetValue((data.WallpaperUid, rtype), out var preview)) {
                            preview.Activate();
                            return;
                        }

                        var jsonString = await _wpControlClient.GetPlayerStartArgsAsync(data, rtype, token);
                        var previewWindow = rtype switch {
                            RuntimeType.RImage or RuntimeType.RImage3D or RuntimeType.RVideo => new PreviewWithWeb(jsonString),
                            _ or RuntimeType.RUnknown => throw new NotImplementedException(),
                        };
                        previewWindow.Closed += (sender, args) => {
                            _previews.Remove((data.WallpaperUid, rtype));
                        };
                        previewWindow.Applied += async (sender, context) => {
                            previewWindow.Close();

                            Grpc_SetWallpaperResponse response = await _wpControlClient.SetWallpaperAsync(
                                _wpSettingsViewModel.Monitors[_wpSettingsViewModel.SelectedMonitorIndex],
                                data,
                                rtype,
                                token);
                            if (!response.IsFinished) {
                                GlobalMessageUtil.ShowError(Constants.I18n.Dialog_Content_ApplyError, isNeedLocalizer: true);
                            }
                        };
                        _previews.Add((data.WallpaperUid, rtype), previewWindow);
                        previewWindow.Show();
                        previewWindow.Activate();
                    }
                    catch (Exception ex) when
                        (ex is OperationCanceledException ||
                        (ex is RpcException rpc && rpc.StatusCode == StatusCode.Cancelled)) {
                        GlobalMessageUtil.ShowCanceled();
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                        GlobalMessageUtil.ShowException(ex);
                    }
                }, cts: ctsPreview);
        }

        internal async Task ApplyAsync(IWpBasicData data) {
            var ctx = ArcPageContextManager.GetContext<WpSettings>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            var ctsApply = new CancellationTokenSource();
            await loadingCtx.RunAsync(
                operation: async token => {
                    try {
                        if (!data.IsAvailable()) return;
                        await CheckFileUpdateAsync(data);

                        var rtype = await GetWallpaperRTypeByFTypeAsync(data.FType);
                        if (rtype == RuntimeType.RUnknown) return;

                        Grpc_SetWallpaperResponse response = await _wpControlClient.SetWallpaperAsync(
                            _wpSettingsViewModel.Monitors[_wpSettingsViewModel.SelectedMonitorIndex],
                            data,
                            rtype,
                            token);
                        if (!response.IsFinished) {
                            GlobalMessageUtil.ShowError(Constants.I18n.Dialog_Content_ApplyError, isNeedLocalizer: true);
                        }
                    }
                    catch (Exception ex) when
                        (ex is OperationCanceledException ||
                        (ex is RpcException rpc && rpc.StatusCode == StatusCode.Cancelled)) {
                        GlobalMessageUtil.ShowCanceled();
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                        GlobalMessageUtil.ShowException(ex);
                    }
                }, cts: ctsApply);
        }

        internal async Task ApplyToLockBGAsync(IWpBasicData data) {
            var ctx = ArcPageContextManager.GetContext<WpSettings>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            var ctsApplyLockBG = new CancellationTokenSource();
            await loadingCtx.RunAsync(
                operation: async token => {
                    try {
                        if (!data.IsAvailable()) return;

                        if (data.FType != FileType.FImage && data.FType != FileType.FGif) {
                            GlobalMessageUtil.ShowError(Constants.I18n.Dialog_Content_OnlyPictureAndGif, isNeedLocalizer: true);
                            return;
                        }

                        StorageFile storageFile = await StorageFile.GetFileFromPathAsync(data.FilePath);
                        await LockScreen.SetImageFileAsync(storageFile);

                        GlobalMessageUtil.ShowSuccess(Constants.I18n.InfobarMsg_Success, isNeedLocalizer: true);
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                        GlobalMessageUtil.ShowException(ex);
                    }
                }, cts: ctsApplyLockBG);
        }

        internal async Task DeleteAsync(IWpBasicData data) {
            try {
                var dialogRes = await GlobalDialogUtils.ShowDialogAsync(
                    LanguageUtil.GetI18n(Constants.I18n.Dialog_Content_LibraryDelete)
                    , LanguageUtil.GetI18n(Constants.I18n.Dialog_Title_Prompt)
                    , LanguageUtil.GetI18n(Constants.I18n.Text_Confirm)
                    , LanguageUtil.GetI18n(Constants.I18n.Text_Cancel));
                if (dialogRes != DialogResult.Primary) return;

                bool isUsing = await IsFileInUseAsync(data);
                if (isUsing) {
                    GlobalMessageUtil.ShowInfo(Constants.I18n.Text_FileUsing, isNeedLocalizer: true);
                    return;
                }

                HandleDelete(data);
                if (Directory.Exists(data.FolderPath)) {
                    Directory.Delete(data.FolderPath, true);
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
            }
        }

        private void HandleDelete(IWpBasicData data) {
            LibraryWallpapers.Remove(data);
            _libraryWallpapers.Remove(data);
            _wallpaperIndexService.Remove(data);
        }

        private void UpdateLib(IWpBasicData data) {
            if (_wallpaperIndexService.TryGetValue(data.WallpaperUid, out int idx)) {
                LibraryWallpapers[idx] = data;
                _libraryWallpapers[idx] = data;
            }
            else {
                LibraryWallpapers.Insert(0, data);
                _libraryWallpapers.Insert(0, data);
            }
            _wallpaperIndexService.Update(data);
        }

        internal async Task DropFilesAsync(IReadOnlyList<IStorageItem> items) {
            var ctx = ArcPageContextManager.GetContext<WpSettings>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            var ctsImport = new CancellationTokenSource();
            await loadingCtx.RunAsync(
                operation: async token => {
                    try {
                        List<ImportValue> importValues = await GetImportValueFromLocalAsync(items);
                        await ImportAsync(importValues);
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                        GlobalMessageUtil.ShowException(ex);
                    }
                }, cts: ctsImport);
        }

        private async Task ImportAsync(List<ImportValue> importValues) {
            var ctx = ArcPageContextManager.GetContext<WpSettings>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            var ctsImport = new CancellationTokenSource();
            int finishedCnt = 0;
            int total = importValues.Count;
            await loadingCtx.RunWithProgressAsync(
                operation: async (token, reportProgress) => {
                    foreach (var importValue in importValues) {
                        try {
                            token.ThrowIfCancellationRequested();

                            if (importValue.FType != FileType.FUnknown) {
                                var grpcData = await _wpControlClient.CreateBasicDataAsync(
                                    importValue.FilePath,
                                    importValue.FType,
                                    token);

                                if (grpcData == null) {
                                    GlobalMessageUtil.ShowError(
                                        Constants.I18n.InfobarMsg_ImportErr,
                                        isNeedLocalizer: true,
                                        extraMsg: importValue.FilePath);
                                    return;
                                }

                                var data = DataAssist.GrpcToBasicData(grpcData);
                                if (data.IsAvailable()) {
                                    UpdateLib(data);
                                }
                                else {
                                    GlobalMessageUtil.ShowError(
                                        Constants.I18n.InfobarMsg_ImportErr,
                                        isNeedLocalizer: true,
                                        extraMsg: importValue.FilePath);
                                }
                            }
                            else {
                                GlobalMessageUtil.ShowError(
                                    Constants.I18n.Dialog_Content_Import_Failed_Lib,
                                    isNeedLocalizer: true,
                                    extraMsg: importValue.FilePath);
                            }

                            reportProgress(++finishedCnt, total);
                        }
                        catch (Exception ex) when (
                            ex is OperationCanceledException ||
                            (ex is RpcException rpc && rpc.StatusCode == StatusCode.Cancelled)) {
                            GlobalMessageUtil.ShowCanceled();
                            return;
                        }
                        catch (Exception ex) {
                            ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                            GlobalMessageUtil.ShowException(ex);
                        }
                    }
                }, total: importValues.Count, cts: ctsImport);
        }

        private async Task<RuntimeType> GetWallpaperRTypeByFTypeAsync(FileType ftype) {
            switch (ftype) {
                case FileType.FImage:
                case FileType.FGif:
                    var wpCreateDialogViewModel = new WallpaperCreateViewModel();
                    var dialogRes = await GlobalDialogUtils.ShowDialogAsync(
                        new WallpaperCreateView(wpCreateDialogViewModel),
                        LanguageUtil.GetI18n(Constants.I18n.Dialog_Title_CreateType),
                        LanguageUtil.GetI18n(Constants.I18n.Text_Confirm),
                        LanguageUtil.GetI18n(Constants.I18n.Text_Cancel));
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

        private async Task CheckFileUpdateAsync(IWpBasicData data) {
            if (data.AppInfo.FileVersion != _userSettingsClient.Settings.FileVersion) {
                var dialogRes = await GlobalDialogUtils.ShowDialogAsync(
                    LanguageUtil.GetI18n(Constants.I18n.Dialog_Content_Import_NeedUpdate),
                    LanguageUtil.GetI18n(Constants.I18n.Dialog_Title_Prompt),
                    LanguageUtil.GetI18n(Constants.I18n.Text_Confirm),
                    LanguageUtil.GetI18n(Constants.I18n.Text_Cancel));
                if (dialogRes == DialogResult.Primary)
                    await UpdateAsync(data);
            }
        }

        private async Task<bool> IsFileInUseAsync(IWpBasicData data) {
            await _userSettingsClient.LoadAsync<List<IWallpaperLayout>>();
            return _userSettingsClient.WallpaperLayouts.Any(wl => wl.FolderPath == data.FolderPath);
        }

        private bool IsFileInPreview(IWpBasicData data) {
            return _previews.Keys.Any(k => k.uid == data.WallpaperUid);
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

        #region filter
        public FilterKey FilterKeyword { get; set; } = FilterKey.LibraryTitle;

        public void ApplyFilter(string keyword) {
            FilterByTitle(keyword);
        }

        public void FilterByTitle(string keyword) {
            var filtered = _libraryWallpapers.Where(basicData =>
                basicData.Title != null && basicData.Title.Contains(keyword, StringComparison.InvariantCultureIgnoreCase)
            );
            Remove_NonMatching(filtered);
            AddBack_Procs(filtered);
        }

        private void Remove_NonMatching(IEnumerable<IWpBasicData> basicDatas) {
            for (int i = LibraryWallpapers.Count - 1; i >= 0; i--) {
                var item = LibraryWallpapers[i];
                if (!basicDatas.Contains(item)) {
                    LibraryWallpapers.Remove(item);
                }
            }
        }

        private void AddBack_Procs(IEnumerable<IWpBasicData> basicDatas) {
            foreach (var item in basicDatas) {
                if (!LibraryWallpapers.Contains(item)) {
                    LibraryWallpapers.Add(item);
                }
            }
        }

        internal async void LoadMoreAsync() {
            try {
                LibLoadingStatus = LoadingStatus.Changing;
                await _wallpaperIndexService.Initialized.Task;

                var entries = _wallpaperIndexService.Query(_offset, _limit);
                foreach (var entry in entries) {
                    var jsonPath = entry.JsonPath;
                    WpBasicData? data = await JsonSaver.LoadAsync<WpBasicData>(jsonPath, WpBasicDataContext.Default);
                    if (data == null || !data.IsAvailable())
                        continue;

                    LibraryWallpapers.Add(data);
                    _libraryWallpapers.Add(data);
                    _offset++;
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
            }
            finally {
                LibLoadingStatus = LoadingStatus.Ready;
            }
        }
        #endregion

        private struct ImportValue(string filePath, FileType ftype) {
            internal string FilePath { get; set; } = filePath;
            internal FileType FType { get; set; } = ftype;
        }

        private int _offset = 0;
        private readonly int _limit = 30;
        private readonly IWallpaperControlClient _wpControlClient;
        private readonly IUserSettingsClient _userSettingsClient;
        private readonly WpSettingsViewModel _wpSettingsViewModel;
        private readonly WallpaperIndexService _wallpaperIndexService;
        private List<string> _wallpaperInstallFolders = [];
        private readonly Dictionary<string, ArcWindow> _details = [];
        private readonly Dictionary<string, ArcWindow> _edits = [];
        private readonly Dictionary<(string uid, RuntimeType rtype), ArcWindow> _previews = [];
        private List<IWpBasicData> _libraryWallpapers = [];
        private bool _isInited;
    }

    public enum LoadingStatus {
        Ready, Changing
    }
}
