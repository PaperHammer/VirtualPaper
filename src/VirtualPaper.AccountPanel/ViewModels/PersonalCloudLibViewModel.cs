using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using VirtualPaper.AccountPanel.Views.Utils;
using VirtualPaper.Common;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Others;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.ViewModels;
using WinUIEx;

namespace VirtualPaper.AccountPanel.ViewModels {
    partial class PersonalCloudLibViewModel : ObservableObject {
        public ObservableList<IWpBasicData> CloudLibWallpapers { get; set; }
        public string MenuFlyout_Text_DetailAndEditInfo { get; set; } = string.Empty;
        public string MenuFlyout_Text_Downlaod { get; set; } = string.Empty;
        public string MenuFlyout_Text_Preview { get; set; } = string.Empty;
        public string MenuFlyout_Text_DeleteFromServer { get; set; } = string.Empty;

        public PersonalCloudLibViewModel(
            IGalleryClient galleryClient,
            IUserSettingsClient userSettingsClient,
            IAccountClient accountClient,
            IWallpaperControlClient wallpaperControlClient) {
            _galleryClient = galleryClient;
            _userSettingsClient = userSettingsClient;
            _wpControlClient = wallpaperControlClient;
            _accountClient = accountClient;

            InitText();
            InitColletions();
        }

        private void InitColletions() {
            CloudLibWallpapers = [];
            _wallpaperInstallFolders = [
                _userSettingsClient.Settings.WallpaperDir,
            ];
        }

        private void InitText() {
            MenuFlyout_Text_DetailAndEditInfo = LanguageUtil.GetI18n(nameof(Constants.I18n.MenuFlyout_Text_DetailAndEditInfo));
            MenuFlyout_Text_Downlaod = LanguageUtil.GetI18n(nameof(Constants.I18n.MenuFlyout_Text_Downlaod));
            MenuFlyout_Text_Preview = LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Preview));
            MenuFlyout_Text_DeleteFromServer = LanguageUtil.GetI18n(nameof(Constants.I18n.Text_DeleteFromServer));
        }

        internal async Task InitContentAsync() {
            try {
                Account.Instance.GetNotify().Loading(false, false);
                CloudLibWallpapers.Clear();
                _uid2idx.Clear();

                var response = await _accountClient.GetPersonalCloudLibAsync();
                if (!response.Success) {
                    Account.Instance.GetNotify().ShowMsg(
                        true,
                        response.Message,
                        InfoBarType.Error,
                        key: response.Message,
                        isAllowDuplication: false);
                    return;
                }

                foreach (var lib in response.Wallpapers) {
                    var data = DataAssist.GrpcToBasicData(lib);
                    UpdateLib(data);
                }
            }
            catch (Exception ex) {
                Account.Instance.GetNotify().ShowExp(ex);
            }
            finally {
                Account.Instance.GetNotify().Loaded();
            }
        }

        private void UpdateLib(WpBasicData data) {
            try {
                ArgumentNullException.ThrowIfNull(nameof(data));
                if (_uid2idx.TryGetValue(data.WallpaperUid, out int idx)) {
                    CloudLibWallpapers[idx] = data;
                }
                else {
                    _uid2idx[data.WallpaperUid] = CloudLibWallpapers.Count;
                    CloudLibWallpapers.Add(data);
                }
            }
            catch (Exception ex) {
                Account.Instance.GetNotify().ShowExp(ex);
            }
        }

        internal async Task DetailAndEditInfoAsync(IWpBasicData data) {
            try {
                await _detailsSemaphoreSlim.WaitAsync();
                if (!data.IsAvailable()) return;

                _ctsDetails = new CancellationTokenSource();
                Account.Instance.GetNotify().Loading(true, false, [_ctsDetails]);

                var response = await _galleryClient.GetWpSourceDataByWpUidAsync(data.WallpaperUid);
                if (!response.Success || response.SourceData.Data.ToByteArray() is not byte[] bytes) {
                    Account.Instance.GetNotify().ShowMsg(
                        true,
                        response.Message,
                        InfoBarType.Error,
                        key: response.Message,
                        isAllowDuplication: false);
                    return;
                }

                string tempDir = Path.Combine(Constants.CommonPaths.TempDir, data.WallpaperUid);
                string tempFilePath = Path.Combine(tempDir, data.WallpaperUid + data.FileExtension);
                data.FilePath = tempFilePath;
                data.FolderPath = tempDir;
                Directory.CreateDirectory(tempDir);
                await File.WriteAllBytesAsync(tempFilePath, response.SourceData.Data.ToByteArray());
                var fileProperty = await _galleryClient.GetFilePropertyAsync(data.FilePath, data.FType);
                data.Resolution = fileProperty.Resolution;
                data.AspectRatio = fileProperty.AspectRatio;
                data.FileSize = fileProperty.FileSize;
                WallpaperEdit we = new(data);
                we.Show();
            }
            catch (RpcException ex) {
                if (ex.StatusCode == StatusCode.Cancelled) {
                    Account.Instance.GetNotify().ShowCanceled();
                }
                else {
                    Account.Instance.GetNotify().ShowExp(ex);
                }
            }
            catch (OperationCanceledException) {
                Account.Instance.GetNotify().ShowCanceled();
            }
            catch (Exception ex) {
                Account.Instance.GetNotify().ShowExp(ex);
            }
            finally {
                Account.Instance.GetNotify().Loaded([_ctsDetails]);
                _detailsSemaphoreSlim.Release();
            }
        }

        internal async Task PreviewAsync(IWpBasicData data) {
            try {
                await _previewSemaphoreSlim.WaitAsync();
                if (!data.IsAvailable()) return;

                _ctsPreview = new CancellationTokenSource();
                Account.Instance.GetNotify().Loading(true, false, [_ctsPreview]);

                var response = await _galleryClient.GetWpSourceDataByWpUidAsync(data.WallpaperUid);
                if (!response.Success || response.SourceData.Data.ToByteArray() is not byte[] bytes) {
                    Account.Instance.GetNotify().ShowMsg(
                        true,
                        response.Message,
                        InfoBarType.Error,
                        key: response.Message,
                        isAllowDuplication: false);
                    return;
                }

                string tempDir = Path.Combine(Constants.CommonPaths.TempDir, data.WallpaperUid);
                string tempFilePath = Path.Combine(tempDir, data.WallpaperUid + data.FileExtension);
                data.FilePath = tempFilePath;
                data.FolderPath = tempDir;
                Directory.CreateDirectory(tempDir);
                await File.WriteAllBytesAsync(tempFilePath, response.SourceData.Data.ToByteArray());
                var fileProperty = await _galleryClient.GetFilePropertyAsync(data.FilePath, data.FType);
                data.Resolution = fileProperty.Resolution;
                data.AspectRatio = fileProperty.AspectRatio;
                data.FileSize = fileProperty.FileSize;
                data.Save();
                var rtype = await GetWallpaperRTypeByFTypeAsync(data.FType);
                if (rtype == RuntimeType.RUnknown) return;

                await _wpControlClient.PreviewWallpaperAsync(data, rtype, _ctsPreview.Token);
            }
            catch (RpcException ex) {
                if (ex.StatusCode == StatusCode.Cancelled) {
                    Account.Instance.GetNotify().ShowCanceled();
                }
                else {
                    Account.Instance.GetNotify().ShowExp(ex);
                }
            }
            catch (OperationCanceledException) {
                Account.Instance.GetNotify().ShowCanceled();
            }
            catch (Exception ex) {
                Account.Instance.GetNotify().ShowExp(ex);
            }
            finally {
                Account.Instance.GetNotify().Loaded([_ctsPreview]);
                _previewSemaphoreSlim.Release();
            }
        }

        internal async Task DownloadAsync(IWpBasicData data) {
            throw new NotImplementedException();
        }

        internal async Task DeleteAsync(IWpBasicData data) {
            try {
                var dialogRes = await Account.Instance.GetDialog().ShowDialogAsync(
                    LanguageUtil.GetI18n(Constants.I18n.Dialog_Content_LibraryDelete)
                    , LanguageUtil.GetI18n(Constants.I18n.Dialog_Title_Prompt)
                    , LanguageUtil.GetI18n(Constants.I18n.Text_Confirm)
                    , LanguageUtil.GetI18n(Constants.I18n.Text_Cancel));
                if (dialogRes != DialogResult.Primary) return;

                var response = await _galleryClient.DeleteWallpaper(data.WallpaperUid);
                if (!response.Success) {
                    Account.Instance.GetNotify().ShowMsg(
                        true,
                        response.Message,
                        InfoBarType.Error,
                        key: response.Message,
                        isAllowDuplication: false);
                    return;
                }

                _uid2idx.Remove(data.WallpaperUid, out _);
                CloudLibWallpapers.Remove(data);
            }
            catch (Exception ex) {
                Account.Instance.GetNotify().ShowExp(ex);
            }
        }

        private async Task<RuntimeType> GetWallpaperRTypeByFTypeAsync(FileType ftype) {
            switch (ftype) {
                case FileType.FImage:
                case FileType.FGif:
                    var wpCreateDialogViewModel = new WallpaperCreateViewModel();
                    var dialogRes = await Account.Instance.GetDialog().ShowDialogAsync(
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

        private readonly IGalleryClient _galleryClient;
        private readonly IAccountClient _accountClient;
        private readonly IWallpaperControlClient _wpControlClient;
        private readonly IUserSettingsClient _userSettingsClient;
        private List<string> _wallpaperInstallFolders;
        private readonly ConcurrentDictionary<string, int> _uid2idx = [];
        private CancellationTokenSource _ctsPreview, _ctsDetails;
        private readonly SemaphoreSlim _previewSemaphoreSlim = new(1, 1);
        private readonly SemaphoreSlim _detailsSemaphoreSlim = new(1, 1);
    }
}
