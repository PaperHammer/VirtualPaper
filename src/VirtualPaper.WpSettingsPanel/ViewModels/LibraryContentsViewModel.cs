using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.ML.DepthEstimate.Interfaces;
using VirtualPaper.ML.DepthEstimate;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.PlayerWeb.Core.WebView.Windows;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Others;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.Adapter;
using VirtualPaper.UIComponent.Utils.Adapter.Interfaces;
using VirtualPaper.UIComponent.Utils.PanelBus.WpSettingsArgs;
using VirtualPaper.UIComponent.ViewModels;
using VirtualPaper.WpSettingsPanel.Utils;
using VirtualPaper.WpSettingsPanel.Utils.Interfaces;
using Windows.Storage;
using Windows.System.UserProfile;
using WinUIEx;

namespace VirtualPaper.WpSettingsPanel.ViewModels {
    public partial class LibraryContentsViewModel : ObservableObject, IFilterable {
        public ObservableCollection<IWpBasicData> LibraryWallpapers { get; private set; } = null!;

        private LoadingStatus _libLoadingStatus;
        public LoadingStatus LibLoadingStatus {
            get { return _libLoadingStatus; }
            set { _libLoadingStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(EndOfResults)); }
        }

        public LibraryContentsViewModel(
            IUserSettingsClient userSettingsClient,
            IWallpaperControlClient wallpaperControlClient,
            WpSettingsViewModel wpSettingsViewModel,
            IWallpaperIndexService wallpaperIndexService,
            IGlobalDialogService? dialogService = null) {
            _userSettingsClient = userSettingsClient;
            _wpControlClient = wallpaperControlClient;
            _wpSettingsViewModel = wpSettingsViewModel;
            _wallpaperIndexService = wallpaperIndexService;
            _dialogService = dialogService ?? new GlobalDialogService();

            InitColletions();
            InitOthers();
        }

        private void InitOthers() {
            _wallpaperIndexService.Initialize(_wallpaperInstallFolders);
            _wpSettingsViewModel.RegisterLibraryContents(this);
            RegisterPanelActions();
        }

        private void InitColletions() {
            _wallpaperInstallFolders = GetWallpaperInstallFolders();
            LibraryWallpapers = [];
            _libraryWallpapers.Clear();
        }

        private string[] GetWallpaperInstallFolders() => WallpaperLibraryDirectories.All
            .Select(directory => directory.GetPath(_userSettingsClient.Settings)).ToArray();

        private async Task RefreshLibraryLocationAsync() {
            var folders = GetWallpaperInstallFolders();
            if (_wallpaperInstallFolders.SequenceEqual(folders, StringComparer.OrdinalIgnoreCase)) return;

            await _wallpaperIndexService.Initialized.Task;
            await _wallpaperIndexService.RebuildAsync(folders);
            _wallpaperInstallFolders = folders;
            LibraryWallpapers.Clear();
            _libraryWallpapers.Clear();
            _offset = 0;
            _loadedFilter = null;
            _isInited = false;
        }

        internal async Task InitContentAsync() {
            await RefreshLibraryLocationAsync();
            if (_isInited) return;
            _isInited = true;
            await ReloadQueryAsync();
        }

        internal void ShowDetail(IWpBasicData data) {
            try {
                if (!data.IsAvailable()) return;

                if (_details.TryGetValue(data.WallpaperUid, out var onlyDetail)) {
                    onlyDetail.Activate();
                    return;
                }

                var onlyDetailWindow = data.FType switch {
                    FileType.FImage or FileType.FGif or FileType.FVideo or FileType.FWebZip => new OnlyDetails(DataConfigTab.GeneralInfo, data),
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

        internal async Task SaveDetailsAsync(IWpBasicData data, WallpaperDetailsEdit edit) {
            if (data.Title != edit.Title || data.Desc != edit.Description || data.Authors != edit.Authors) {
                var draft = data.Clone();
                draft.AppInfo = new() { AppName = data.AppInfo.AppName, AppVersion = data.AppInfo.AppVersion, FileVersion = data.AppInfo.FileVersion };
                draft.Title = edit.Title;
                draft.Desc = edit.Description;
                draft.Authors = edit.Authors;
                await draft.SaveAsync();
                data.Title = draft.Title;
                data.Desc = draft.Desc;
                data.Authors = draft.Authors;
                data.AppInfo = draft.AppInfo;
                _wallpaperIndexService.Update(data);
            }
            // Metadata and personal preferences are separate files. Refresh persisted
            // metadata even if saving preferences fails, so a retry cannot show a stale index.
            try {
                await _wallpaperIndexService.UpdateOrganizationAsync([data.WallpaperUid], tags: [edit.PersonalTags]);
                OrganizationChanged?.Invoke(this, EventArgs.Empty);
            }
            finally {
                await RefreshLibraryAsync();
            }
        }

        internal static IReadOnlyList<IWpBasicData> ContextItems(IWpBasicData target, IEnumerable<IWpBasicData> selected) {
            var items = selected.ToArray();
            return items.Any(item => string.Equals(item.WallpaperUid, target.WallpaperUid, StringComparison.OrdinalIgnoreCase)) ? items : [target];
        }

        internal (bool CanAdd, bool CanRemove) FavoriteActions(IEnumerable<IWpBasicData> items) {
            bool canAdd = false, canRemove = false;
            foreach (var item in items) {
                if (IsFavorite(item)) canRemove = true;
                else canAdd = true;
            }
            return (canAdd, canRemove);
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
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                        GlobalMessageUtil.ShowException(ex);
                    }
                });
        }

        internal async Task PreviewAsync(IWpBasicData data, ArcPageContext? ctx) {
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

                        string? depthFilePath = rtype == RuntimeType.RImage3D
                            ? await SetDepthPathAsync(data, token) : null;
                        token.ThrowIfCancellationRequested();
                        var jsonString = await _wpControlClient.GetPlayerStartArgsAsync(data, rtype, depthFilePath, token);
                        token.ThrowIfCancellationRequested();
                        // Another request may have opened this preview while inference was running.
                        if (_previews.TryGetValue((data.WallpaperUid, rtype), out preview)) {
                            preview.Activate();
                            return;
                        }

                        var previewWindow = rtype switch {
                            RuntimeType.RImage or RuntimeType.RImage3D or RuntimeType.RVideo or RuntimeType.RWeb => new PreviewWithWeb(jsonString),
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
                                isFromPreview: true,
                                depthFilePath,
                                context.WpEffectFilePathUsing,
                                context.WpEffectFilePathTemplate,
                                context.WpEffectFilePathTemporary,
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

                        string? depthFilePath = null;
                        if (rtype == RuntimeType.RImage3D) {
                            depthFilePath = await SetDepthPathAsync(data, token);
                        }
                        
                        Grpc_SetWallpaperResponse response = await _wpControlClient.SetWallpaperAsync(
                            _wpSettingsViewModel.Monitors[_wpSettingsViewModel.SelectedMonitorIndex],
                            data,
                            rtype,
                            isFromPreview: false,
                            depthFilePath,
                            null, null, null,
                            token);
                        if (!response.IsFinished) {
                            GlobalMessageUtil.ShowError(Constants.I18n.Dialog_Content_ApplyError, isNeedLocalizer: true);
                        }
                        else {
                            _wpSettingsViewModel.UpdateAdjustWindowCache();
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

        private static Task<string> SetDepthPathAsync(IWpBasicData data, CancellationToken token) =>
            DepthMapCache.GetOrCreateAsync(data.FilePath, data.FolderPath,
                () => AppServiceLocator.Services.GetRequiredService<IDepthEstimate>(), token);

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
            await DeleteAsync(new[] { data });
        }

        internal async Task DeleteAsync(IReadOnlyCollection<IWpBasicData> items) {
            try {
                var uniqueItems = items
                    .GroupBy(item => item.WallpaperUid)
                    .Select(group => group.First())
                    .ToList();
                if (uniqueItems.Count == 0) return;

                string title = uniqueItems.Count == 1
                    ? LanguageUtil.GetI18n(Constants.I18n.Dialog_Title_LibraryDelete)
                    : string.Format(
                        LanguageUtil.GetI18n(Constants.I18n.Dialog_Title_LibraryDeleteMany),
                        uniqueItems.Count);
                string message = uniqueItems.Count == 1
                    ? LanguageUtil.GetI18n(Constants.I18n.Dialog_Content_LibraryDelete)
                    : LanguageUtil.GetI18n(Constants.I18n.Dialog_Content_LibraryDeleteMany);
                var dialogRes = await _dialogService.ShowDialogAsync(
                    message,
                    title,
                    LanguageUtil.GetI18n(Constants.I18n.Text_Delete),
                    LanguageUtil.GetI18n(Constants.I18n.Text_Cancel));
                if (dialogRes != DialogResult.Primary) return;

                await _userSettingsClient.LoadAsync<List<IWallpaperLayout>>();
                var activeFolders = _userSettingsClient.WallpaperLayouts
                    .Select(layout => layout.FolderPath)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var deletable = uniqueItems
                    .Where(item => !activeFolders.Contains(item.FolderPath))
                    .ToList();
                if (deletable.Count == 0) {
                    GlobalMessageUtil.ShowInfo(Constants.I18n.Text_FileUsing, isNeedLocalizer: true);
                    return;
                }

                HandleDelete(deletable);
                foreach (string folderPath in deletable
                    .Select(item => item.FolderPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)) {
                    if (Directory.Exists(folderPath)) {
                        Directory.Delete(folderPath, true);
                    }
                }
                if (deletable.Count != uniqueItems.Count) {
                    GlobalMessageUtil.ShowInfo(Constants.I18n.Text_FileUsing, isNeedLocalizer: true);
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
            }
        }

        public void HandleDelete(IWpBasicData data) {
            HandleDelete(new[] { data });
        }

        public void HandleDelete(IEnumerable<IWpBasicData> items) {
            bool handledAny = false;
            foreach (var data in items
                .GroupBy(item => item.WallpaperUid)
                .Select(group => group.First())) {
                LibraryWallpapers.Remove(data);
                _libraryWallpapers.Remove(data.WallpaperUid);
                _wallpaperIndexService.Remove(data);
                handledAny = true;
            }
            if (handledAny) {
                if (_isInited) _ = ReloadQueryAsync();
                ItemDeleted?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>删除条目后触发，通知 View 层检查是否需要补充加载更多数据</summary>
        public event EventHandler? ItemDeleted;

        public void UpdateLib(IWpBasicData data) {
            UpdateLibraryEntry(data);
            _ = RefreshLibraryAsync();
        }

        internal async Task UpdateLibAsync(IEnumerable<IWpBasicData> items) {
            bool changed = false;
            foreach (var data in items) {
                UpdateLibraryEntry(data);
                changed = true;
            }
            if (changed) await RefreshLibraryAsync();
        }

        private void UpdateLibraryEntry(IWpBasicData data) {
            // Older Core messages did not carry creation time. Keep the known date
            // so a metadata refresh cannot send an existing card to the end.
            if (data.CreatedTime == default && _libraryWallpapers.TryGetValue(data.WallpaperUid, out var existing))
                data.CreatedTime = existing.CreatedTime;
            _libraryWallpapers[data.WallpaperUid] = data;
            _wallpaperIndexService.Update(data);
            // Keep per-wallpaper notifications; refresh the list once for the batch.
            PanelMessageCenter.Publish(
                PanelContracts.WpSettings.Id,
                PanelContracts.WpSettings.Event_WallpaperImported,
                data.FolderPath);
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
            var imported = new List<IWpBasicData>();
            try {
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
                                        imported.Add(data);
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
            finally {
                // Publish successful imports even if a later item fails or is cancelled.
                await UpdateLibAsync(imported);
            }
        }

        private async Task<RuntimeType> GetWallpaperRTypeByFTypeAsync(FileType ftype) {
            switch (ftype) {
                case FileType.FImage:
                case FileType.FGif:
                    var wpCreateDialogViewModel = new WallpaperCreateViewModel();
                    var dialog = GlobalDialogUtils.CreateDialog(
                        new WallpaperCreateView(wpCreateDialogViewModel),
                        LanguageUtil.GetI18n(Constants.I18n.Dialog_Title_CreateType),
                        LanguageUtil.GetI18n(Constants.I18n.Text_Confirm),
                        LanguageUtil.GetI18n(Constants.I18n.Text_Cancel))!;
                    dialog.DialogWidth = 480;
                    dialog.DialogMaxWidth = 480;
                    var dialogRes = await dialog.ShowAsync();
                    if (dialogRes != ContentDialogResult.Primary) return RuntimeType.RUnknown;

                    return wpCreateDialogViewModel.SelectedItem.CreateType switch {
                        WallpaperCreateType.Img => RuntimeType.RImage,
                        WallpaperCreateType.DepthImg => RuntimeType.RImage3D,
                        _ => RuntimeType.RUnknown,
                    };
                case FileType.FVideo:
                    return RuntimeType.RVideo;
                case FileType.FWebZip:
                    return RuntimeType.RWeb;
                default:
                    return RuntimeType.RUnknown;
            }
        }

        private async Task CheckFileUpdateAsync(IWpBasicData data) {
            if (data.AppInfo.FileVersion != _userSettingsClient.Settings.FileVersion) {
                var dialogRes = await GlobalDialogUtils.ShowDialogAsync(
                    LanguageUtil.GetI18n(Constants.I18n.Dialog_Content_Import_NeedUpdate),
                    LanguageUtil.GetI18n(Constants.I18n.Dialog_Title_LibraryUpgrade),
                    LanguageUtil.GetI18n(Constants.I18n.Text_Upgrade),
                    LanguageUtil.GetI18n(Constants.I18n.Text_Cancel));
                if (dialogRes == DialogResult.Primary)
                    await UpdateAsync(data);
            }
        }

        public async Task<bool> IsFileInUseAsync(IWpBasicData data) {
            return await _wpControlClient.GetRunMonitorByWallpaperAsync(data.WallpaperUid) != null;
        }

        public bool IsFileInPreview(IWpBasicData data) {
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

        public void RegisterPanelActions() {
            // Action：入库
            PanelMessageCenter.RegisterAction<string, bool>(
                PanelContracts.WpSettings.Id,
                PanelContracts.WpSettings.Action_ImportWallpaper,
                async (filePath) => {
                    try {
                        var ftype = FileFilter.GetFileType(filePath);
                        if (ftype == FileType.FUnknown) return false;

                        var grpcData = await _wpControlClient.CreateBasicDataAsync(filePath, ftype, CancellationToken.None);
                        if (grpcData == null) return false;

                        var data = DataAssist.GrpcToBasicData(grpcData);
                        if (!data.IsAvailable()) return false;

                        UpdateLib(data);
                        return true;
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                        return false;
                    }
                });

            // Action：预览（无需入库，直接打开预览窗口）
            PanelMessageCenter.RegisterAction<PreviewFileArgs, bool>(
                PanelContracts.WpSettings.Id,
                PanelContracts.WpSettings.Action_PreviewFile,
                async (args) => {
                    try {
                        var ftype = FileFilter.GetFileType(args.FilePath);
                        if (ftype == FileType.FUnknown) return false;

                        var grpcData = await _wpControlClient.CreateBasicDataAsync(args.FilePath, ftype, CancellationToken.None);
                        if (grpcData == null) return false;

                        var data = DataAssist.GrpcToBasicData(grpcData);
                        if (!data.IsAvailable()) return false;

                        await PreviewAsync(data, args.Ctx);
                        return true;
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                        return false;
                    }
                });
        }

        #region filter
        public FilterKey FilterKeyword { get; set; } = FilterKey.LibraryTitle;

        public void ApplyFilter(string keyword) {
            ApplyFilter(new FilterContext { TitleKeyword = keyword });
        }

        public void ApplyFilter(FilterContext context) {
            bool textChanged = _filter.TitleKeyword != context.TitleKeyword || _filter.Tag != context.Tag;
            _filter = context;
            if (_isInited) _ = ReloadQueryAsync(debounce: textChanged);
            else ApplyLoadedFilter();
        }

        public void FilterByTitle(string keyword) {
            ApplyFilter(new FilterContext { TitleKeyword = keyword });
        }

        private void ApplyLoadedFilter() {
            var entries = WallpaperLibraryQuery.Apply(_libraryWallpapers.Values.Select(WallpaperIndexEntry.FromData), _filter, GetOrganization);
            ReplaceVisible(entries.Take(Math.Max(_limit, LibraryWallpapers.Count)).Select(entry => _libraryWallpapers[entry.Uid]).ToArray());
        }

        private void ReplaceVisible(IReadOnlyList<IWpBasicData> items) {
            var wanted = items.Select(item => item.WallpaperUid).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var existingItems = LibraryWallpapers.ToDictionary(item => item.WallpaperUid, StringComparer.OrdinalIgnoreCase);
            for (int i = LibraryWallpapers.Count - 1; i >= 0; i--) {
                if (!wanted.Contains(LibraryWallpapers[i].WallpaperUid)) LibraryWallpapers.RemoveAt(i);
            }
            for (int i = 0; i < items.Count; i++) {
                if (i < LibraryWallpapers.Count && ReferenceEquals(LibraryWallpapers[i], items[i])) continue;
                int existing = existingItems.TryGetValue(items[i].WallpaperUid, out var existingItem)
                    ? LibraryWallpapers.IndexOf(existingItem) : -1;
                if (existing >= 0) {
                    if (existing != i) LibraryWallpapers.Move(existing, i);
                    if (!ReferenceEquals(LibraryWallpapers[i], items[i])) LibraryWallpapers[i] = items[i];
                }
                else {
                    LibraryWallpapers.Insert(i, items[i]);
                }
            }
            while (LibraryWallpapers.Count > items.Count) LibraryWallpapers.RemoveAt(LibraryWallpapers.Count - 1);
            OnPropertyChanged(nameof(IsEmpty));
        }

        public bool IsEmpty => LibraryWallpapers.Count == 0 && LibLoadingStatus != LoadingStatus.Changing;
        public bool EndOfResults => !_hasMore && !IsEmpty && LibLoadingStatus != LoadingStatus.Changing;
        public WallpaperOrganization GetOrganization(string uid) => _wallpaperIndexService.GetOrganization(uid) ?? new();
        public bool IsFavorite(IWpBasicData data) => GetOrganization(data.WallpaperUid).Favorite;
        public event EventHandler? OrganizationChanged;
        public event EventHandler? QueryReset;

        public async Task OrganizeAsync(IEnumerable<IWpBasicData> items, bool? favorite = null,
            IEnumerable<string>? tags = null, WallpaperTagUpdate mode = WallpaperTagUpdate.Replace) {
            await _wallpaperIndexService.UpdateOrganizationAsync(items.Select(item => item.WallpaperUid), favorite, tags, mode);
            OrganizationChanged?.Invoke(this, EventArgs.Empty);
            // A favorite toggle cannot change this result set unless favorites are filtered.
            if (favorite != null && tags == null && !_filter.FavoritesOnly) return;
            await RefreshLibraryAsync();
        }

        private async Task RefreshLibraryAsync() {
            if (_isInited) await ReloadQueryAsync();
            else {
                ApplyLoadedFilter();
                RefreshTags();
            }
        }

        private void RefreshTags() => _wpSettingsViewModel.UpdateTags(_wallpaperIndexService.GetTags() ?? []);

        internal async Task ApplyFilterAsync(FilterContext filter, bool debounce = false) {
            _filter = filter;
            _isInited = true;
            await ReloadQueryAsync(debounce);
        }

        private async Task ReloadQueryAsync(bool debounce = false) {
            int version = unchecked(++_queryVersion);
            LibLoadingStatus = LoadingStatus.Changing;
            if (debounce) await Task.Delay(180);
            // Superseded input never reaches the index, disk or visible collection.
            if (version != _queryVersion) return;
            await _queryGate.WaitAsync();
            try {
                await _wallpaperIndexService.Initialized.Task;
                if (version != _queryVersion) return;
                await LoadPageAsync(version, reset: true);
                RefreshTags();
            }
            catch (Exception ex) {
                ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
            }
            finally {
                if (version == _queryVersion) LibLoadingStatus = LoadingStatus.Ready;
                _queryGate.Release();
            }
        }

        internal async void LoadMoreAsync() => await LoadMorePageAsync();

        internal async Task LoadMorePageAsync() {
            if (!_hasMore || LibLoadingStatus == LoadingStatus.Changing) return;
            int version = _queryVersion;
            LibLoadingStatus = LoadingStatus.Changing;
            await _queryGate.WaitAsync();
            try {
                await _wallpaperIndexService.Initialized.Task;
                if (version == _queryVersion) await LoadPageAsync(version, reset: false);
            }
            catch (Exception ex) { ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex); GlobalMessageUtil.ShowException(ex); }
            finally {
                if (version == _queryVersion) LibLoadingStatus = LoadingStatus.Ready;
                _queryGate.Release();
            }
        }

        private async Task LoadPageAsync(int version, bool reset) {
            int offset = reset ? 0 : _offset;
            var filter = _filter;
            // Data updates keep the loaded range; only a different filter resets scrolling.
            bool filterChanged = _loadedFilter != filter;
            int pageSize = reset && !filterChanged ? Math.Max(_limit, LibraryWallpapers.Count) : _limit;
            var page = new List<IWpBasicData>();
            bool more = true;
            while (page.Count < pageSize && more) {
                int requested = pageSize - page.Count;
                var entries = _wallpaperIndexService.Query(offset, requested, filter);
                more = entries.Count == requested;
                foreach (var entry in entries) {
                    offset++; // Consume invalid/missing metadata too; never get stuck on one page.
                    if (version != _queryVersion) return;
                    try {
                        _libraryWallpapers.TryGetValue(entry.Uid, out var data);
                        if (data != null && !Path.Combine(data.FolderPath, Constants.Field.WpBasicDataFileName)
                            .Equals(entry.JsonPath, StringComparison.OrdinalIgnoreCase)) data = null;
                        data ??= await WpBasicData.LoadAsync(entry.JsonPath);
                        if (version != _queryVersion) return;
                        if (data?.IsAvailable() != true) continue;
                        page.Add(data);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or Errors.FileAccessException) {
                        ArcLog.GetLogger<LibraryContentsViewModel>().Warn($"Skipped wallpaper metadata: {entry.JsonPath}. {ex.Message}");
                    }
                }
            }
            if (version != _queryVersion) return;
            _offset = offset;
            _hasMore = more;
            if (reset) {
                ReplaceVisible(page);
                _loadedFilter = filter;
                if (filterChanged) QueryReset?.Invoke(this, EventArgs.Empty);
            }
            else {
                var visible = LibraryWallpapers.Select(item => item.WallpaperUid).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var item in page) {
                    if (visible.Add(item.WallpaperUid)) LibraryWallpapers.Add(item);
                }
            }
            foreach (var item in page) {
                _libraryWallpapers[item.WallpaperUid] = item;
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
        private readonly IWallpaperIndexService _wallpaperIndexService;
        private readonly IGlobalDialogService _dialogService;
        private string[] _wallpaperInstallFolders = [];
        private readonly Dictionary<string, ArcWindow> _details = [];
        private readonly Dictionary<(string uid, RuntimeType rtype), ArcWindow> _previews = [];
        private readonly Dictionary<string, IWpBasicData> _libraryWallpapers = new(StringComparer.OrdinalIgnoreCase);
        private bool _isInited;
        private FilterContext _filter = FilterContext.Empty;
        private FilterContext? _loadedFilter;
        private readonly SemaphoreSlim _queryGate = new(1, 1);
        private int _queryVersion;
        private bool _hasMore = true;

        /// <summary>仅供单元测试使用</summary>
        public void TestPopulate(IEnumerable<IWpBasicData> items) {
            foreach (var item in items) {
                _libraryWallpapers[item.WallpaperUid] = item;
                LibraryWallpapers.Add(item);
            }
        }
    }

    public enum LoadingStatus {
        Ready, Changing
    }
}
