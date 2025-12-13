using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Grpc.Core;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.PlayerWeb;
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
using VirtualPaper.PlayerWeb.Core;
using VirtualPaper.PlayerWeb.Core.WebView.Windows;
using VirtualPaper.UIComponent.Container;
using VirtualPaper.UIComponent.Others;
using VirtualPaper.UIComponent.Templates;
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
            var ctx = ArcPageContextManager.GetContext<WpSettings>();
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
                        GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
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

                var jsonString = GetStartArgsString(data);
                var onlyDetailWindow = data.FType switch {
                    FileType.FImage or FileType.FGif or FileType.FVideo => new OnlyDetails(jsonString, DataConfigTab.GeneralInfo),
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
                GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
            }
        }

        private string GetStartArgsString(IWpBasicData data) {
            var args = new StartArgsWeb() {
                FilePath = data.FilePath,
                WpBasicDataFilePath = Path.Combine(data.FolderPath, Constants.Field.WpBasicDataFileName),                
                ApplicationTheme = ArcThemeUtil.MainWindowAppTheme,
                SystemBackdrop = ArcThemeUtil.MainWindowBackdrop,
                Language = LanguageUtil.CurrentLanguage,
            };

            var json = JsonSerializer.Serialize(args, StartArgsWebContext.Default.StartArgsWeb);

            return json;
        }

        internal async Task EditInfoAsync(IWpBasicData data) {
            //try {
            //    if (!data.IsAvailable()) return;
            //    await CheckFileUpdateAsync(data);

            //    if (_wp2TocEdit.TryGetValue(data.FilePath, out ToolWindow toolWindow)) {
            //        toolWindow.BringToFront();
            //        return;
            //    }

            //    var mainWindow = WindowConsts.ArcWindowInstance;
            //    toolWindow = new ToolWindow(new(
            //        _userSettingsClient.Settings.SystemBackdrop,
            //        _userSettingsClient.Settings.ApplicationTheme,
            //        _userSettingsClient.Settings.Language));
            //    toolWindow.Closed += ToolContainer_Closed;
            //    void ToolContainer_Closed(object _, WindowEventArgs __) {
            //        toolWindow.Closed -= ToolContainer_Closed;
            //        Edits edits = toolWindow.GetContentByTag($"Edits_{data.FilePath}") as Edits;
            //        if (edits.IsSaved) {
            //            data.Title = edits.Title;
            //            data.Desc = edits.Desc;
            //            data.Tags = string.Join(';', edits.TagList);
            //            data.Save();
            //            UpdateLib(data);
            //            GlobalMessageUtil.ShowSuccess(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), Constants.I18n.InfobarMsg_Success, isNeedLocalizer: true);
            //        }

            //        _wp2TocEdit.Remove(data.FilePath);
            //    }

            //    SetToolWindowParent(toolWindow, mainWindow);
            //    _wp2TocEdit[data.FilePath] = toolWindow;
            //    toolWindow.Show();
            //}
            //catch (Exception ex) {
            //    ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
            //    GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
            //}
            try {
                if (!data.IsAvailable()) return;

                if (_details.TryGetValue(data.WallpaperUid, out var editDetail)) {
                    editDetail.Activate();
                    return;
                }

                var jsonString = GetStartArgsString(data);
                var editDetailWindow = data.FType switch {
                    FileType.FImage or FileType.FGif or FileType.FVideo => new OnlyDetails(jsonString, DataConfigTab.GeneralInfoEdit),
                    _ => throw new NotImplementedException(),
                };
                editDetailWindow.Closed += (sender, args) => {
                    _details.Remove(data.WallpaperUid);
                };
                _details.Add(data.WallpaperUid, editDetailWindow);
                editDetailWindow.Show();
                editDetailWindow.Activate();
            }
            catch (Exception ex) {
                ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
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
                            GlobalMessageUtil.ShowInfo(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), message: Constants.I18n.Text_FileUsing, isNeedLocalizer: true);
                            return;
                        }

                        bool isPreview = IsFileInPreview(data);
                        if (isPreview) {
                            GlobalMessageUtil.ShowInfo(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), message: Constants.I18n.Text_FileInPreview, isNeedLocalizer: true);
                            return;
                        }

                        Grpc_WpBasicData grpc_basicData = await _wpControlClient.UpdateBasicDataAsync(data.FolderPath, data.FolderName, data.FilePath, data.FType)
                            ?? throw new Exception("File update failed.");
                        data = DataAssist.GrpcToBasicData(grpc_basicData);
                        UpdateLib(data);

                        GlobalMessageUtil.ShowSuccess(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), message: Constants.I18n.InfobarMsg_Success, isNeedLocalizer: true);
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                        GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
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
                        await _previewSemaphoreSlim.WaitAsync(token);
                        if (!data.IsAvailable()) return;
                        await CheckFileUpdateAsync(data);

                        var rtype = await GetWallpaperRTypeByFTypeAsync(data.FType);
                        if (rtype == RuntimeType.RUnknown) return;

                        if (_previews.TryGetValue((data.WallpaperUid, rtype), out var preview)) {
                            preview.Activate();
                            return;
                        }

                        var jsonString = await _wpControlClient.GetPlayerStartArgsAsync(_wpSettingsViewModel.SelectedMonitor.DeviceId, data, rtype, token);
                        var previewWindow = rtype switch {
                            RuntimeType.RImage or RuntimeType.RImage3D or RuntimeType.RVideo => new PreviewWithWeb(jsonString),
                            _ or RuntimeType.RUnknown => throw new NotImplementedException(),
                        };
                        previewWindow.Closed += (sender, args) => {
                            _previews.Remove((data.WallpaperUid, rtype));
                        };
                        _previews.Add((data.WallpaperUid, rtype), previewWindow);
                        previewWindow.Show();
                        previewWindow.Activate();
                    }
                    catch (Exception ex) when
                        (ex is OperationCanceledException ||
                        (ex is RpcException rpc && rpc.StatusCode == StatusCode.Cancelled)) {
                        GlobalMessageUtil.ShowCanceled(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)));
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                        GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
                    }
                    finally {
                        _previewSemaphoreSlim.Release();
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
                            _wpSettingsViewModel.SelectedMonitor,
                            data,
                            rtype,
                            token);
                        if (!response.IsFinished) {
                            GlobalMessageUtil.ShowError(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), Constants.I18n.Dialog_Content_ApplyError, isNeedLocalizer: true);
                        }
                    }
                    catch (Exception ex) when
                        (ex is OperationCanceledException ||
                        (ex is RpcException rpc && rpc.StatusCode == StatusCode.Cancelled)) {
                        GlobalMessageUtil.ShowCanceled(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)));
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                        GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
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
                            GlobalMessageUtil.ShowError(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), Constants.I18n.Dialog_Content_OnlyPictureAndGif, isNeedLocalizer: true);
                            return;
                        }

                        StorageFile storageFile = await StorageFile.GetFileFromPathAsync(data.FilePath);
                        await LockScreen.SetImageFileAsync(storageFile);

                        GlobalMessageUtil.ShowSuccess(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), Constants.I18n.InfobarMsg_Success, isNeedLocalizer: true);
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                        GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
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
                    GlobalMessageUtil.ShowInfo(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), Constants.I18n.Text_FileUsing, isNeedLocalizer: true);
                    return;
                }

                _uid2idx.Remove(data.WallpaperUid, out _);
                LibraryWallpapers.Remove(data);
                DirectoryInfo di = new(data.FolderPath);
                di.Delete(true);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
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
                GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
            }
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
                        await ImportFromValuesAsync(importValues);
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                        GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
                    }
                }, cts: ctsImport);
        }

        private async Task ImportFromValuesAsync(List<ImportValue> importValues) {
            var ctx = ArcPageContextManager.GetContext<WpSettings>();
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
                                        ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)),
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
                                        ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)),
                                        Constants.I18n.InfobarMsg_ImportErr,
                                        isNeedLocalizer: true,
                                        extraMsg: importValue.FilePath);
                            }
                            else {
                                GlobalMessageUtil.ShowError(
                                    ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)),
                                    Constants.I18n.Dialog_Content_Import_Failed_Lib,
                                    isNeedLocalizer: true,
                                    extraMsg: importValue.FilePath);
                            }

                            reportProgress(++finishedCnt, total);
                        }
                        catch (Exception ex) when (
                            ex is OperationCanceledException ||
                            (ex is RpcException rpc && rpc.StatusCode == StatusCode.Cancelled)) {
                            GlobalMessageUtil.ShowCanceled(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)));
                            return;
                        }
                        catch (Exception ex) {
                            ArcLog.GetLogger<LibraryContentsViewModel>().Error(ex);
                            GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
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
        private readonly SemaphoreSlim _previewSemaphoreSlim = new(1, 1);
        private static readonly Dictionary<string, ArcWindow> _details = [];
        private static readonly Dictionary<string, ToolWindow> _wp2TocEdit = [];
        private static readonly Dictionary<(string uid, RuntimeType rtype), ArcWindow> _previews = [];
    }
}
