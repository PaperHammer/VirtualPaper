//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;
//using Google.Protobuf.Reflection;
//using Grpc.Core;
//using Microsoft.Extensions.DependencyInjection;
//using VirtualPaper.Common;
//using VirtualPaper.DataAssistor;
//using VirtualPaper.Grpc.Client.Interfaces;
//using VirtualPaper.Grpc.Service.Models;
//using VirtualPaper.Models.Cores;
//using VirtualPaper.Models.Cores.Interfaces;
//using VirtualPaper.Models.Mvvm;
//using VirtualPaper.UI.Services.Interfaces;
//using VirtualPaper.UI.Utils;
//using VirtualPaper.UI.ViewModels.Utils;
//using VirtualPaper.UI.Views.Utils;
//using Windows.Storage;
//using WinUI3Localizer;
//using static VirtualPaper.UI.Services.Interfaces.IDialogService;

//namespace VirtualPaper.UI.ViewModels.WpSettingsComponents {
//    public delegate void ApplyHandler(bool isApply);

//    public partial class WpConfigViewModel : ObservableObject {
//        public event EventHandler UpdateWebviewContent;
//        public event ApplyHandler Applied;
//        public event EventHandler WpEffectSendMsg;

//        public string TextUpdateWallpaper { get; set; } = string.Empty;
//        public string TextWpConfigCustomize { get; set; } = string.Empty;
//        public string TextResolution { get; set; } = string.Empty;
//        public string TextAspectRatio { get; set; } = string.Empty;
//        public string TextType { get; set; } = string.Empty;
//        public string TextFileExtension { get; set; } = string.Empty;
//        public string TextFileSize { get; set; } = string.Empty;
//        public string TextDetailedInfo { get; set; } = string.Empty;

//        private IWpMetadata _wallpaper;
//        public IWpMetadata Wallpaper {
//            get => _wallpaper;
//            set {
//                _wallpaper = value;
//                OnPropertyChanged();
//                UpdateWebviewContent?.Invoke(this, EventArgs.Empty);
//            }
//        }

//        private bool _isWpEffectVisible;
//        public bool IsWpEffectVisible {
//            get { return _isWpEffectVisible; }
//            set { _isWpEffectVisible = value; OnPropertyChanged(); }
//        }

//        public WpConfigViewModel(
//            IDialogService dialogService,
//            IUserSettingsClient userSettingsClient,
//            IWallpaperControlClient wpControlClient,
//            WpSettingsViewModel wpSettingsViewModel) {
//            _dialogService = dialogService;
//            _userSettingsClient = userSettingsClient;
//            _wpControlClient = wpControlClient;
//            _wpSettingsViewModel = wpSettingsViewModel;

//            InitText();
//        }

//        private void InitText() {
//            _localizer = Localizer.Get();

//            TextUpdateWallpaper = _localizer.GetLocalizedString(Constants.LocalText.WpConfigViewMdoel_TextUpdateWallpaper);
//            TextResolution = _localizer.GetLocalizedString(Constants.LocalText.WpConfigViewMdoel_TextResolution);
//            TextAspectRatio = _localizer.GetLocalizedString(Constants.LocalText.WpConfigViewMdoel_TextAspectRatio);
//            TextType = _localizer.GetLocalizedString(Constants.LocalText.WpConfigViewMdoel_TextType);
//            TextFileExtension = _localizer.GetLocalizedString(Constants.LocalText.WpConfigViewMdoel_TextFileExtension);
//            TextFileSize = _localizer.GetLocalizedString(Constants.LocalText.WpConfigViewMdoel_TextFileSize);
//            TextDetailedInfo = _localizer.GetLocalizedString(Constants.LocalText.WpConfigViewMdoel_TextDetailedInfo);
//        }

//        internal async Task RestoreWallpaperAsync() {
//            try {
//                BasicUIComponentUtil.Loading(false, false, null);

//                var data = await Task.Run(async () => {
//                    WallpaperBasicData wpBasicData;
//                    IMonitor selectedMonitor = _wpSettingsViewModel.GetSelectedMonitor();
//                    if (selectedMonitor.Content == "Expand" || selectedMonitor.Content == "Duplicate") {
//                        wpBasicData = _wpControlClient.Wallpapers.FirstOrDefault();
//                    }
//                    else {
//                        wpBasicData = _wpControlClient.Wallpapers.FirstOrDefault(x => x.Monitor.Content == selectedMonitor.Content);
//                    }

//                    WpMetadata data = null;
//                    if (wpBasicData != null) {
//                        var grpcData = await _wpControlClient.GetWallpaperAsync(wpBasicData.FolderPath);
//                        data = new() {
//                            BasicData = DataAssist.GrpcToBasicData(grpcData.WpBasicData),
//                            RuntimeData = DataAssist.GrpcToRuntimeData(grpcData.WpRuntimeData),
//                        };
//                    }

//                    return data;
//                });

//                if (data != null) {
//                    File.Copy(data.RuntimeData.WpEffectFilePathUsing, data.RuntimeData.WpEffectFilePathTemporary, true);

//                    Wallpaper = new WpMetadata() {
//                        BasicData = data.BasicData,
//                        RuntimeData = data.RuntimeData,
//                    };
//                }
//                else {
//                    Wallpaper = null;
//                    IsWpEffectVisible = false;
//                }
//            }
//            catch (Exception ex) {
//                BasicUIComponentUtil.ShowExp(ex);
//                Wallpaper = null;
//                IsWpEffectVisible = false;
//            }
//            finally {
//                BasicUIComponentUtil.Loaded(null);
//                //if (App.IsNeedReslease)
//                //{
//                //    App.LibSemaphoreSlim.Release();
//                //    App.IsNeedReslease = false;
//                //}
//            }
//        }

//        internal async Task DropFileAsync(IReadOnlyList<IStorageItem> items) {
//            try {
//                ImportValue importValue = WallpaperUtil.ImportSingleFile(items);

//                await ImportFromLocalAsync(importValue);
//            }
//            catch (Exception ex) {
//                BasicUIComponentUtil.ShowExp(ex);
//            }
//        }

//        internal async Task ImportFromLocalAsync(ImportValue importValue) {
//            try {
//                if (importValue.FType == FileType.FUnknown) {
//                    await _dialogService.ShowDialogAsync(
//                       $"\"{importValue.FilePath}\"\n" + _localizer.GetLocalizedString(Constants.LocalText.Dialog_Content_Import_Failed_Ctrl)
//                       , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Title_Prompt)
//                       , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm));
//                    return;
//                }

//                _ctsImport = new CancellationTokenSource();
//                BasicUIComponentUtil.Loading(true, false, [_ctsImport]);

//                var data = new WpMetadata();

//                var wpMetadataBasic = await _wpControlClient.CreateBasicDataAsync(
//                    Constants.CommonPaths.TempDir,
//                    importValue.FilePath,
//                    importValue.FType,
//                    _ctsImport.Token);
//                data.BasicData = DataAssist.GrpcToBasicData(wpMetadataBasic);

//                await ImportAsync(data);
//            }
//            catch (OperationCanceledException) {
//                BasicUIComponentUtil.ShowCanceled();
//            }
//            catch (Exception ex) {
//                BasicUIComponentUtil.ShowExp(ex);
//                Wallpaper = null;
//                IsWpEffectVisible = false;
//            }
//            finally {
//                BasicUIComponentUtil.Loaded([_ctsImport]);
//            }
//        }

//        internal async Task ImportFromLibAsync(IWpMetadata data) {
//            try {
//                _ctsImport = new CancellationTokenSource();
//                BasicUIComponentUtil.Loading(false, false, [_ctsImport]);

//                await ImportAsync(data);
//            }
//            catch (OperationCanceledException) {
//                BasicUIComponentUtil.ShowCanceled();
//            }
//            catch (Exception ex) {
//                BasicUIComponentUtil.ShowExp(ex);
//                Wallpaper = null;
//                IsWpEffectVisible = false;
//            }
//            finally {
//                BasicUIComponentUtil.Loaded([_ctsImport]);
//            }
//        }

//        internal async void Close() {
//            Wallpaper = null;
//            IsWpEffectVisible = false;

//            await _wpControlClient.CloseWallpaperAsync(_wpSettingsViewModel.GetSelectedMonitor());
//        }

//        internal async Task PreviewAsync() {
//            if (Wallpaper == null) return;

//            await _wpControlClient.PreviewWallpaperAsync(Wallpaper, true);
//        }

//        internal async Task ModifyPreviewAsync(string controlName, string propertyName, string val) {
//            await _wpControlClient.ModifyPreviewAsync(controlName, propertyName, val);
//        }

//        internal async void Apply() {
//            try {
//                if (Wallpaper == null) return;

//                BasicUIComponentUtil.Loading(true, false, [_ctsApply]);
//                _ctsApply = new CancellationTokenSource();

//                #region 生成运行时效果文件
//                string wpEffectFilePathusing = await _wpControlClient.CreateRuntimeDataUsingAsync(
//                   Wallpaper.RuntimeData.FolderPath,
//                   Wallpaper.RuntimeData.WpEffectFilePathTemplate,
//                   _wpSettingsViewModel.GetSelectedMonitor().Content,
//                   (int)_userSettingsClient.Settings.WallpaperArrangement);
//                Wallpaper.RuntimeData.WpEffectFilePathUsing = wpEffectFilePathusing;
//                #endregion

//                #region 本地导入时录入信息
//                var detailedInfoViewModel = new DetailedInfoViewModel(Wallpaper, true, false, false);
//                var dialogRes = await _dialogService.ShowDialogWithoutTitleAsync(
//                    new DetailedInfoView(detailedInfoViewModel)
//                    , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm));
//                if (dialogRes != DialogResult.Primary) return;
//                #endregion

//                #region 应用修改（避免第一次导入时的修改无法生效）
//                Applied?.Invoke(true);
//                #endregion

//                #region 拷贝入库并更新数据
//                Wallpaper.BasicData.Title = detailedInfoViewModel.Title;
//                Wallpaper.BasicData.Desc = detailedInfoViewModel.Desc;
//                Wallpaper.BasicData.Tags = detailedInfoViewModel.Tags;
//                string tagetFolder = Path.Combine(
//                    _userSettingsClient.Settings.WallpaperDir,
//                    Wallpaper.BasicData.FolderName);
//                if (Wallpaper.BasicData.FolderPath != tagetFolder) {
//                    Wallpaper.MoveTo(tagetFolder);
//                    App.Services.GetRequiredService<LibraryContentsViewModel>().UpdateLib(Wallpaper);
//                }
//                #endregion

//                #region 执行操作
//                // 同一显示器 同一壁纸 更改自定义设置
//                if (_wpControlClient.Wallpapers.FirstOrDefault(x => x.WallPaperUid == Wallpaper.BasicData.WallpaperUid) != null) {
//                    WpEffectSendMsg?.Invoke(this, EventArgs.Empty);
//                    return;
//                }

//                // 同一显示器 更换壁纸
//                var selectedMonitor = _wpSettingsViewModel.GetSelectedMonitor();
//                if (_wpControlClient.Wallpapers.FirstOrDefault(x => x.Monitor.DeviceId == selectedMonitor.DeviceId) != null) {
//                    await _wpControlClient.UpdateWallpaperAsync(
//                        selectedMonitor,
//                        Wallpaper,
//                        _ctsApply.Token);
//                    return;
//                }

//                // 对某一显示器第一次应用壁纸
//                Grpc_SetWallpaperResponse setUesponse = await _wpControlClient.SetWallpaperAsync(
//                   selectedMonitor,
//                   Wallpaper,
//                    _ctsApply.Token);
//                if (!setUesponse.IsFinished) {
//                    await _dialogService.ShowDialogAsync(
//                        _localizer.GetLocalizedString(Constants.LocalText.Dialog_Content_ApplyError)
//                        , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Title_Error)
//                        , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm));
//                }
//                #endregion
//            }
//            catch (OperationCanceledException) {
//                BasicUIComponentUtil.ShowCanceled();
//            }
//            catch (RpcException ex) {
//                if (ex.StatusCode == StatusCode.Cancelled) BasicUIComponentUtil.ShowCanceled();
//                else BasicUIComponentUtil.ShowExp(ex);
//            }
//            catch (Exception ex) {
//                BasicUIComponentUtil.ShowExp(ex);
//            }
//            finally {
//                BasicUIComponentUtil.Loaded([_ctsApply]);
//            }
//        }

//        internal async Task ShowDetailedInfoAsync() {
//            try {
//                if (Wallpaper == null) return;

//                var detailedInfoViewModel = new DetailedInfoViewModel(Wallpaper, false, false, false);
//                _ = await _dialogService.ShowDialogWithoutTitleAsync(
//                    new DetailedInfoView(detailedInfoViewModel)
//                    , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm));
//            }
//            catch (Exception ex) {
//                BasicUIComponentUtil.ShowExp(ex);
//            }
//        }

//        private async Task ImportAsync(IWpMetadata data) {
//            await SetRuntimeDataAsync(data);

//            Wallpaper = data;
//        }

//        internal async Task SetRuntimeDataAsync(IWpMetadata data) {
//            var rtype = await SeleceWallpaperRuntimeTypeAsync(data.BasicData.FType);
//            if (rtype == RuntimeType.FUnknown) return;

//            data.RuntimeData.RType = rtype;

//            var wpMetadataRuntime = await _wpControlClient.CreateRuntimeDataAsync(
//                data.BasicData.FolderPath,
//                data.RuntimeData.RType,
//                _ctsImport.Token);
//            data.RuntimeData = DataAssist.GrpcToRuntimeData(wpMetadataRuntime);
//        }

//        private async Task<RuntimeType> SeleceWallpaperRuntimeTypeAsync(FileType ftype) {
//            var rtype = RuntimeType.FUnknown;
//            switch (ftype) {
//                case FileType.FPicture:
//                case FileType.FGif:
//                    var wpCreateDialogViewModel = new WallpaperCreateDialogViewModel();
//                    var dialogRes = await _dialogService.ShowDialogAsync(
//                        new WallpaperCreateView(wpCreateDialogViewModel)
//                        , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Title_CreateType)
//                        , _localizer.GetLocalizedString(Constants.LocalText.Dialog_Btn_Confirm));
//                    if (dialogRes != DialogResult.Primary) return RuntimeType.FUnknown;

//                    return wpCreateDialogViewModel.SelectedItem.CreateType switch {
//                        WallpaperCreateType.Img => RuntimeType.RImage,
//                        WallpaperCreateType.DepthImg => RuntimeType.RImage3D,
//                        _ => RuntimeType.FUnknown,
//                    };
//                case FileType.FVideo:
//                    return RuntimeType.FVideo;
//            }

//            return rtype;
//        }

//        private ILocalizer _localizer;
//        private readonly IDialogService _dialogService;
//        private readonly IUserSettingsClient _userSettingsClient;
//        private readonly IWallpaperControlClient _wpControlClient;
//        private CancellationTokenSource _ctsImport;
//        private CancellationTokenSource _ctsApply;
//        private readonly WpSettingsViewModel _wpSettingsViewModel;
//    }
//}
