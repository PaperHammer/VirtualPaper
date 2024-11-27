using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
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
using VirtualPaper.UIComponent.Utils;
using Windows.Storage;
using Windows.System.UserProfile;
using WinUI3Localizer;
using static VirtualPaper.UI.Services.Interfaces.IDialogService;

namespace VirtualPaper.UI.ViewModels.WpSettingsComponents {
    public partial class LibraryContentsViewModel : ObservableObject {
        public ObservableCollection<IWpMetadata> LibraryWallpapers { get; set; }
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

        internal async Task InitContentsAsync() {
            try {
                BasicUIComponentUtil.Loading(false, false, null);
                LibraryWallpapers.Clear();

                await foreach (var libData in WallpaperUtil.ImportWallpaperByFoldersAsync(_wallpaperInstallFolders)) {
                    _uid2idx[libData.Data.BasicData.WallpaperUid] = libData.Idx;
                    LibraryWallpapers.Add(libData.Data);
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

        internal async Task DetailedInfoAsync(IWpMetadata data) {
            try {
                bool isAvailable = await CheckFileAvailableAsync(data);
                if (!isAvailable) return;

                var detailedInfoViewModel = new DetailedInfoViewModel(data, false, true, false);
                var detailedInfoView = new DetailedInfoView(detailedInfoViewModel);
                var dialogRes = await _dialogService.ShowDialogWithoutTitleAsync(
                    detailedInfoView,
                    _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm));

                if (dialogRes != DialogResult.Primary) return;
            }
            catch (Exception ex) {
                BasicUIComponentUtil.ShowExp(ex);
            }
        }

        internal async Task UpdateAsync(IWpMetadata data) {
            try {
                _ctsUpdate = new CancellationTokenSource();
                BasicUIComponentUtil.Loading(true, false, [_ctsUpdate]);

                Grpc_WpMetaData grpc_data = await _wpControlClient.UpdateFileDataAsync(data, _ctsUpdate.Token)
                    ?? throw new Exception("File repair failed.");
                data.BasicData = DataAssist.GrpcToBasicData(grpc_data.WpBasicData);
                if (data.BasicData.IsSingleRType) {
                    data.RuntimeData = DataAssist.GrpcToRuntimeData(grpc_data.WpRuntimeData);
                }
                UpdateLib(data);

                bool isUsing = await CheckFileUsingAsync(data, true);
                if (isUsing) {
                    Grpc_MonitorData grpc_monitor = await _wpControlClient.GetRunMonitorByWallpaperAsync(data.BasicData.WallpaperUid);
                    IMonitor monitor = DataAssist.GrpToMonitorData(grpc_monitor);
                    await _wpControlClient.SetWallpaperAsync(monitor, data, default);
                }

                BasicUIComponentUtil.ShowMsg(true, Constants.LocalText.InfobarMsg_Success, InfoBarSeverity.Success);
            }
            catch (Exception ex) {
                BasicUIComponentUtil.ShowExp(ex);
            }
            finally {
                BasicUIComponentUtil.Loaded([_ctsUpdate]);
            }
        }

        internal async Task EditInfoAsync(IWpMetadata data) {
            try {
                bool isAvailable = await CheckFileAvailableAsync(data);
                if (!isAvailable) return;

                var detailedInfoViewModel = new DetailedInfoViewModel(data, true, true, false);
                var detailedInfoView = new DetailedInfoView(detailedInfoViewModel);
                var dialogRes = await _dialogService.ShowDialogWithoutTitleAsync(
                    detailedInfoView,
                    _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm));

                if (dialogRes != DialogResult.Primary) return;

                data.BasicData.Title = detailedInfoViewModel.Title;
                data.BasicData.Desc = detailedInfoViewModel.Desc;
                data.BasicData.Save();
                UpdateLib(data);
            }
            catch (Exception ex) {
                BasicUIComponentUtil.ShowExp(ex);
            }
        }

        internal async Task PreviewAsync(IWpMetadata data) {
            try {
                await _previewSemaphoreSlim.WaitAsync();
                bool isAvailable = await CheckFileAvailableAsync(data);
                if (!isAvailable) return;
                
                _ctsPreview = new CancellationTokenSource();
                BasicUIComponentUtil.Loading(true, false, [_ctsPreview]);

                isAvailable = await PreRun(data, _ctsPreview);
                if (!isAvailable) return;              

                bool isStarted = await _wpControlClient.PreviewWallpaperAsync(data);
                if (!isStarted) {
                    throw new Exception("Preview Failed.");
                }
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

        internal async Task ApplyAsync(IWpMetadata data) {
            try {
                await _applySemaphoreSlim.WaitAsync();
                bool isAvailable = await CheckFileAvailableAsync(data);
                if (!isAvailable) return;
                
                _ctsApply = new CancellationTokenSource();
                BasicUIComponentUtil.Loading(true, false, [_ctsApply]);

                isAvailable = await PreRun(data, _ctsApply);
                if (!isAvailable) return;

                #region 本地导入时录入信息
                var detailedInfoViewModel = new DetailedInfoViewModel(data, true, false, false);
                var dialogRes = await _dialogService.ShowDialogWithoutTitleAsync(
                    new DetailedInfoView(detailedInfoViewModel)
                    , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm));
                if (dialogRes != DialogResult.Primary) return;
                #endregion

                #region 拷贝入库并更新数据
                data.BasicData.Title = detailedInfoViewModel.Title;
                data.BasicData.Desc = detailedInfoViewModel.Desc;
                data.BasicData.Tags = detailedInfoViewModel.Tags;
                data.Save();
                #endregion

                #region 执行操作
                // 具体应该如何实现交给 core 判断，不应该由 ui 层决定
                Grpc_SetWallpaperResponse setUesponse = await _wpControlClient.SetWallpaperAsync(
                    _wpSettingsViewModel.GetSelectedMonitor(),
                    data,
                    _ctsApply.Token);
                if (!setUesponse.IsFinished) {
                    BasicUIComponentUtil.ShowMsg(
                        true,
                        Constants.LocalText.Dialog_Content_ApplyError,
                        InfoBarSeverity.Error);
                }

                //// 同一显示器 同一壁纸 更改自定义设置
                //if (_wpControlClient.Wallpapers.FirstOrDefault(x => x.WallPaperUid == data.BasicData.WallpaperUid) != null) {
                //    WpEffectSendMsg?.Invoke(this, EventArgs.Empty);
                //    return;
                //}

                //// 同一显示器 更换壁纸
                //var selectedMonitor = _wpSettingsViewModel.GetSelectedMonitor();
                //if (_wpControlClient.Wallpapers.FirstOrDefault(x => x.Monitor.DeviceId == selectedMonitor.DeviceId) != null) {
                //    await _wpControlClient.UpdateWallpaperAsync(
                //        selectedMonitor,
                //        Wallpaper,
                //        _ctsApply.Token);
                //    return;
                //}

                //// 对某一显示器第一次应用壁纸
                //Grpc_SetWallpaperResponse setUesponse = await _wpControlClient.SetWallpaperAsync(
                //   selectedMonitor,
                //   Wallpaper,
                //    _ctsApply.Token);
                //if (!setUesponse.IsFinished) {
                //    await _dialogService.ShowDialogAsync(
                //        _localizer.GetLocalizedString(Constants.LocalText.Dialog_Content_ApplyError)
                //        , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Title_Error)
                //        , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm));
                //}
                #endregion

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

        internal async Task ApplyToLockBGAsync(IWpMetadata data) {
            try {
                _ctsApplyLockBG = new CancellationTokenSource();
                BasicUIComponentUtil.Loading(true, false, [_ctsApplyLockBG]);
                bool isExists = await CheckFileAvailableAsync(data);
                if (!isExists) return;

                if (data.BasicData.FType != FileType.FPicture && data.BasicData.FType != FileType.FGif) {
                    await _dialogService.ShowDialogAsync(
                        _localizer.GetLocalizedString(Constants.LocalText.Dialog_Content_OnlyPictureAndGif)
                        , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Title_Prompt)
                        , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm));
                    return;
                }

                StorageFile storageFile = await StorageFile.GetFileFromPathAsync(data.BasicData.FilePath);
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

        internal async Task DeleteAsync(IWpMetadata data) {
            try {
                var dialogRes = await _dialogService.ShowDialogAsync(
                    _localizer.GetLocalizedString(Constants.LocalText.Dialog_Content_LibraryDelete)
                    , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Title_Prompt)
                    , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm)
                    , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Cancel));
                if (dialogRes != DialogResult.Primary) return;

                bool isUsing = await CheckFileUsingAsync(data, false);
                if (isUsing) return;

                string uid = data.BasicData.WallpaperUid;
                _uid2idx.Remove(uid, out _);
                LibraryWallpapers.Remove(data);
                DirectoryInfo di = new(data.BasicData.FolderPath);
                di.Delete(true);
            }
            catch (Exception ex) {
                BasicUIComponentUtil.ShowExp(ex);
            }
        }

        private void UpdateLib(IWpMetadata data) {
            try {
                ArgumentNullException.ThrowIfNull(nameof(data));
                lock (_lockAddToLib) {
                    if (_uid2idx.TryGetValue(data.BasicData.WallpaperUid, out int idx)) {
                        LibraryWallpapers[idx] = data;
                    }
                    else {
                        _uid2idx[data.BasicData.WallpaperUid] = LibraryWallpapers.Count;
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
                List<ImportValue> importValues = await WallpaperUtil.ImportMultipleFileAsync(items);
                await ImportToLibFromLocalAsync(importValues);
            }
            finally {
                BasicUIComponentUtil.Loaded([_ctsImport]);
            }
        }

        private async Task ImportToLibFromLocalAsync(List<ImportValue> importValues) {
            try {
                int finishedCnt = 0;
                BasicUIComponentUtil.UpdateProgressbarValue(0, importValues.Count);

                foreach (var importValue in importValues) {
                    try {
                        if (importValue.FType != FileType.FUnknown) {
                            #region 初始化基础信息
                            WpMetadata wallpaper = new();

                            string tagetFolder = Path.Combine(
                                _userSettingsClient.Settings.WallpaperDir,
                                wallpaper.BasicData.FolderName);

                            await SetBasicDataAsync(importValue, wallpaper, tagetFolder, _ctsImport);
                            if (wallpaper.BasicData.IsSingleRType) {
                                await SetRuntimeDataAsync(wallpaper, _ctsImport);
                                if (wallpaper.RuntimeData.RType == RuntimeType.RUnknown)
                                    return;
                            }
                            #endregion

                            #region 展示读取结果
                            var detailedInfoViewModel = new DetailedInfoViewModel(
                                wallpaper, true, false, false);
                            var dialogRes = await _dialogService.ShowDialogWithoutTitleAsync(
                                new DetailedInfoView(detailedInfoViewModel)
                                , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm));
                            if (dialogRes != DialogResult.Primary) return;

                            wallpaper.BasicData.Title = detailedInfoViewModel.Title;
                            wallpaper.BasicData.Desc = detailedInfoViewModel.Desc;
                            wallpaper.BasicData.Tags = detailedInfoViewModel.Tags;
                            #endregion

                            #region 存入库中                            
                            wallpaper.Save();
                            UpdateLib(wallpaper);
                            #endregion
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

        private async Task SetBasicDataAsync(ImportValue importValue, IWpMetadata data, string tagetFolder, CancellationTokenSource token) {
            var wpMetadataBasic = await _wpControlClient.CreateBasicDataAsync(
                tagetFolder,
                importValue.FilePath,
                importValue.FType,
                token.Token);
            data.BasicData = DataAssist.GrpcToBasicData(wpMetadataBasic);
        }

        private async Task SetRuntimeDataAsync(IWpMetadata data, CancellationTokenSource token) {
            var rtype = await SetWallpaperRuntimeTypeAsync(data.BasicData.FType);
            if (rtype == RuntimeType.RUnknown) return;

            var wpMetadataRuntime = await _wpControlClient.CreateRuntimeDataAsync(
                data.BasicData.FilePath,
                data.BasicData.FolderPath,
                rtype,
                token.Token);
            data.RuntimeData = DataAssist.GrpcToRuntimeData(wpMetadataRuntime);
        }

        private async Task<RuntimeType> SetWallpaperRuntimeTypeAsync(FileType ftype) {
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

            await foreach (var libData in WallpaperUtil.ImportWallpaperByFoldersAsync(_wallpaperInstallFolders)) {
                var md = libData.Data;
                LibraryWallpapers.Add(md);
            }
        }

        private async Task<bool> CheckFileAvailableAsync(IWpMetadata data) {
            if (data == null || !File.Exists(data.BasicData.FilePath)) {
                BasicUIComponentUtil.ShowMsg(true, Constants.LocalText.Dialog_Content_FileNotExists, InfoBarSeverity.Error);
                return false;
            }

            if (data.BasicData.AppInfo.FileVersion != _userSettingsClient.Settings.FileVersion
                || data.RuntimeData.RType != RuntimeType.RUnknown && data.RuntimeData.AppInfo.FileVersion != _userSettingsClient.Settings.FileVersion) {
                var dialogRes = await _dialogService.ShowDialogAsync(
                    _localizer.GetLocalizedString(Constants.LocalText.Dialog_Content_Import_NeedUpdate)
                    , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Title_Prompt)
                    , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm)
                    , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Cancel));
                if (dialogRes == DialogResult.Primary)
                    await UpdateAsync(data);
            }

            return true;
        }

        private async Task<bool> CheckFileUsingAsync(IWpMetadata data, bool isSlient) {
            await _userSettingsClient.LoadAsync<List<IWallpaperLayout>>();
            foreach (var wl in _userSettingsClient.WallpaperLayouts) {
                if (wl.FolderPath == data.BasicData.FolderPath) {
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

        private async Task<bool> PreRun(IWpMetadata data, CancellationTokenSource token) {
            #region 若是非单一 RType 则先选择运行类型
            if (!data.BasicData.IsSingleRType) {
                await SetRuntimeDataAsync(data, token);

                if (data.RuntimeData.RType == RuntimeType.RUnknown) return false;
            }
            #endregion

            #region 生成运行时效果文件
            string wpEffectFilePathusing = await _wpControlClient.CreateRuntimeDataUsingAsync(
               data.RuntimeData.FolderPath,
               data.RuntimeData.WpEffectFilePathTemplate,
               _wpSettingsViewModel.GetSelectedMonitorContent());
            data.RuntimeData.WpEffectFilePathUsing = wpEffectFilePathusing;
            #endregion

            return true;
        }

        private readonly ILocalizer _localizer;
        private readonly IDialogService _dialogService;
        private readonly IWallpaperControlClient _wpControlClient;
        private readonly IUserSettingsClient _userSettingsClient;
        private readonly WpSettingsViewModel _wpSettingsViewModel;
        private List<string> _wallpaperInstallFolders;
        private CancellationTokenSource _ctsImport, _ctsUpdate, _ctsApply, _ctsApplyLockBG, _ctsPreview;
        private readonly ConcurrentDictionary<string, int> _uid2idx = [];
        private readonly static object _lockAddToLib = new();
        private readonly SemaphoreSlim _importSemaphoreSlim = new(1, 1);
        private readonly SemaphoreSlim _applySemaphoreSlim = new(1, 1);
        private readonly SemaphoreSlim _previewSemaphoreSlim = new(1, 1);
    }
}
