using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.UI.Xaml;
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
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Container;
using VirtualPaper.UIComponent.Data;
using VirtualPaper.UIComponent.Logging;
using VirtualPaper.UIComponent.Others;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.ViewModels;
using Windows.Storage;
using Windows.System.UserProfile;
using WinRT.Interop;
using WinUIEx;

namespace VirtualPaper.WpSettingsPanel.ViewModels {
    public partial class LibraryContentsViewModel : ObservableObject {
        public ObservableCollection<IWpBasicData> LibraryWallpapers { get; set; } = [];

        public LibraryContentsViewModel(
            IUserSettingsClient userSettingsClient,
            IWallpaperControlClient wallpaperControlClient,
            WpSettingsViewModel wpSettingsViewModel) {
            _userSettingsClient = userSettingsClient;
            _wpControlClient = wallpaperControlClient;
            _wpSettingsViewModel = wpSettingsViewModel;

            InitColletions();
        }

        private void InitColletions() {
            _wallpaperInstallFolders = [
                _userSettingsClient.Settings.WallpaperDir,
            ];
        }

        internal async Task InitContentAsync() {
            var ctx = PageContextManager.GetContext<WpSettings>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            await loadingCtx.RunAsync(
                operation: async token => {
                    try {
                        LibraryWallpapers.Clear();
                        _uid2idx.Clear();

                        var loader = new AsyncLoader<IWpBasicData>(maxDegreeOfParallelism: 10, channelCapacity: 100);
                        await foreach (var data in loader.LoadItemsAsync(ProcessFolders, _wallpaperInstallFolders, token)) {
                            UpdateLib(data);
                        }
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                        GlobalMessageUtil.ShowException(ex);
                    }
                });
        }

        internal async Task DetailInfoAsync(IWpBasicData data) {
            try {
                if (!data.IsAvailable()) return;
                await CheckFileUpdateAsync(data);

                if (_wp2TocDetail.TryGetValue(data.FilePath, out ToolWindow toolWindow)) {
                    toolWindow.BringToFront();
                    return;
                }

                var mainWindow = WindowConsts.ArcWindowInstance;
                toolWindow = new ToolWindow(new(
                    _userSettingsClient.Settings.SystemBackdrop,
                    _userSettingsClient.Settings.ApplicationTheme,
                    _userSettingsClient.Settings.Language));
                toolWindow.Closed += ToolContainer_Closed;
                void ToolContainer_Closed(object _, WindowEventArgs __) {
                    toolWindow.Closed -= ToolContainer_Closed;
                    _wp2TocDetail.Remove(data.FilePath);
                }

                SetToolWindowParent(toolWindow, mainWindow);
                AddDetailsPage(data, toolWindow);
                _wp2TocDetail[data.FilePath] = toolWindow;
                toolWindow.Show();
            }
            catch (Exception ex) {
                ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
            }
        }

        internal async Task EditInfoAsync(IWpBasicData data) {
            try {
                if (!data.IsAvailable()) return;
                await CheckFileUpdateAsync(data);

                if (_wp2TocEdit.TryGetValue(data.FilePath, out ToolWindow toolWindow)) {
                    toolWindow.BringToFront();
                    return;
                }

                var mainWindow = WindowConsts.ArcWindowInstance;
                toolWindow = new ToolWindow(new(
                    _userSettingsClient.Settings.SystemBackdrop,
                    _userSettingsClient.Settings.ApplicationTheme,
                    _userSettingsClient.Settings.Language));
                toolWindow.Closed += ToolContainer_Closed;
                void ToolContainer_Closed(object _, WindowEventArgs __) {
                    toolWindow.Closed -= ToolContainer_Closed;
                    Edits edits = toolWindow.GetContentByTag($"Edits_{data.FilePath}") as Edits;
                    if (edits.IsSaved) {
                        data.Title = edits.Title;
                        data.Desc = edits.Desc;
                        data.Tags = string.Join(';', edits.TagList);
                        data.Save();
                        UpdateLib(data);
                        GlobalMessageUtil.ShowSuccess(Constants.I18n.InfobarMsg_Success, isNeedLocalizer: true);
                    }

                    _wp2TocEdit.Remove(data.FilePath);
                }

                SetToolWindowParent(toolWindow, mainWindow);
                AddEditsPage(data, toolWindow);
                _wp2TocEdit[data.FilePath] = toolWindow;
                toolWindow.Show();
            }
            catch (Exception ex) {
                ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
            }
        }

        internal async Task UpdateAsync(IWpBasicData data) {
            var ctx = PageContextManager.GetContext<WpSettings>();
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

                        Grpc_WpBasicData grpc_basicData = await _wpControlClient.UpdateBasicDataAsync(data.FolderPath, data.FolderName, data.FilePath, data.FType)
                            ?? throw new Exception("File update failed.");
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
            var ctx = PageContextManager.GetContext<WpSettings>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            var ctsPreview = new CancellationTokenSource();
            await loadingCtx.RunAsync(
                operation: async token => {
                    try {
                        await _previewSemaphoreSlim.WaitAsync(token);
                        if (!data.IsAvailable()) return;

                        await CheckFileUpdateAsync(data);

                        var rtype = await GetWallpaperRTypeByFTypeAsync(data.FType);
                        if (rtype == RuntimeType.RUnknown) return;
                        await _wpControlClient.PreviewWallpaperAsync(_wpSettingsViewModel.SelectedMonitor.DeviceId, data, rtype, token);
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
                    finally {
                        _previewSemaphoreSlim.Release();
                    }
                }, cts: ctsPreview);
        }

        internal async Task ApplyAsync(IWpBasicData data) {
            var ctx = PageContextManager.GetContext<WpSettings>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            var ctsApply = new CancellationTokenSource();
            await loadingCtx.RunAsync(
                operation: async token => {
                    try {
                        await _applySemaphoreSlim.WaitAsync(token);
                        if (!data.IsAvailable()) return;
                        await CheckFileUpdateAsync(data);

                        var rtype = await GetWallpaperRTypeByFTypeAsync(data.FType);
                        if (rtype == RuntimeType.RUnknown) return;

                        Grpc_SetWallpaperResponse response = await _wpControlClient.SetWallpaperAsync(
                            _wpSettingsViewModel.SelectedMonitor,
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
                    finally {
                        _applySemaphoreSlim.Release();
                    }
                }, cts: ctsApply);
        }

        internal async Task ApplyToLockBGAsync(IWpBasicData data) {
            var ctx = PageContextManager.GetContext<WpSettings>();
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
                    finally {
                        _applySemaphoreSlim.Release();
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

                _uid2idx.Remove(data.WallpaperUid, out _);
                LibraryWallpapers.Remove(data);
                DirectoryInfo di = new(data.FolderPath);
                di.Delete(true);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
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
                ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
            }
        }

        internal async Task DropFilesAsync(IReadOnlyList<IStorageItem> items) {
            var ctx = PageContextManager.GetContext<WpSettings>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            var ctsImport = new CancellationTokenSource();
            await loadingCtx.RunAsync(
                operation: async token => {
                    try {
                        List<ImportValue> importValues = await GetImportValueFromLocalAsync(items);
                        await ImportFromValuesAsync(importValues);
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                        GlobalMessageUtil.ShowException(ex);
                    }
                }, cts: ctsImport);
        }

        private async Task ImportFromValuesAsync(List<ImportValue> importValues) {
            var ctx = PageContextManager.GetContext<WpSettings>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            var ctsImport = new CancellationTokenSource();
            await loadingCtx.RunWithProgressAsync(
                operation: async (token, reportProgress) => {
                    int finishedCnt = 0;
                    int total = importValues.Count;

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

                                if (data.IsAvailable())
                                    UpdateLib(data);
                                else
                                    GlobalMessageUtil.ShowError(
                                        Constants.I18n.InfobarMsg_ImportErr,
                                        isNeedLocalizer: true,
                                        extraMsg: importValue.FilePath);
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

        public async Task<bool> IsFileInUseAsync(IWpBasicData data) {
            await _userSettingsClient.LoadAsync<List<IWallpaperLayout>>();
            return _userSettingsClient.WallpaperLayouts.Any(wl => wl.FolderPath == data.FolderPath);
        }

        private static void AddDetailsPage(IWpBasicData data, ToolWindow toolContainer) {
            Details details = new(data);
            toolContainer.AddContent(Constants.I18n.Text_Details, $"Details_{data.FilePath}", details);
        }

        private static void AddEditsPage(IWpBasicData data, ToolWindow toolContainer) {
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
                            WpBasicData data = await JsonSaver.LoadAsync<WpBasicData>(file, WpBasicDataContext.Default);

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

        private readonly IWallpaperControlClient _wpControlClient;
        private readonly IUserSettingsClient _userSettingsClient;
        private readonly WpSettingsViewModel _wpSettingsViewModel;
        private List<string> _wallpaperInstallFolders = [];
        private readonly ConcurrentDictionary<string, int> _uid2idx = [];
        private readonly SemaphoreSlim _applySemaphoreSlim = new(1, 1);
        private readonly SemaphoreSlim _previewSemaphoreSlim = new(1, 1);
        private static readonly Dictionary<string, ToolWindow> _wp2TocDetail = [];
        private static readonly Dictionary<string, ToolWindow> _wp2TocEdit = [];
    }
}
