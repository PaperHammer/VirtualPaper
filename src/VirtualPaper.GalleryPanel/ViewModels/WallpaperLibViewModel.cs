using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using VirtualPaper.Common;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UIComponent.Others;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.ViewModels;

namespace VirtualPaper.GalleryPanel.ViewModels {
    class WallpaperLibViewModel {
        public ObservableCollection<IWpBasicData> GalleryWallpapers { get; set; } = [];

        public WallpaperLibViewModel(
            IGalleryClient galleryClient,
            IWallpaperControlClient wpControlClient) {
            _galleryClient = galleryClient;
            _wpControlClient = wpControlClient;
        }

        internal async Task InitContentAsync() {
            await AsyncGetContent();
        }

        internal async Task SearchContentAsync(string searchKey) {
            await AsyncGetContent(searchKey);
        }

        private async Task AsyncGetContent(string searchKey = "") {
            try {
                Gallery.Instance.GetNotify().Loading(false, false);
                GalleryWallpapers.Clear();

                var response = await _galleryClient.GetCloudLibAsync(searchKey);
                if (!response.Success) {
                    Gallery.Instance.GetNotify().ShowMsg(
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
                Gallery.Instance.GetNotify().ShowExp(ex);
            }
            finally {
                Gallery.Instance.GetNotify().Loaded();
            }
        }

        private void UpdateLib(IWpBasicData data) {
            try {
                ArgumentNullException.ThrowIfNull(nameof(data));
                GalleryWallpapers.Add(data);
            }
            catch (Exception ex) {
                Gallery.Instance.GetNotify().ShowExp(ex);
            }
        }

        internal async Task PreviewAsync(IWpBasicData data) {
            try {
                await _previewSemaphoreSlim.WaitAsync();
                if (!data.IsAvailable()) return;

                _ctsPreview = new CancellationTokenSource();
                Gallery.Instance.GetNotify().Loading(true, false, [_ctsPreview]);

                var response = await _galleryClient.GetWpSourceDataByWpUidAsync(data.WallpaperUid);
                if (!response.Success || response.SourceData.Data.ToByteArray() is not byte[] bytes) {
                    Gallery.Instance.GetNotify().ShowMsg(
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
                    Gallery.Instance.GetNotify().ShowCanceled();
                }
                else {
                    Gallery.Instance.GetNotify().ShowExp(ex);
                }
            }
            catch (OperationCanceledException) {
                Gallery.Instance.GetNotify().ShowCanceled();
            }
            catch (Exception ex) {
                Gallery.Instance.GetNotify().ShowExp(ex);
            }
            finally {
                Gallery.Instance.GetNotify().Loaded([_ctsPreview]);
                _previewSemaphoreSlim.Release();
            }
        }

        private async Task<RuntimeType> GetWallpaperRTypeByFTypeAsync(FileType ftype) {
            switch (ftype) {
                case FileType.FImage:
                case FileType.FGif:
                    var wpCreateDialogViewModel = new WallpaperCreateViewModel();
                    var dialogRes = await Gallery.Instance.GetDialog().ShowDialogAsync(
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
        private readonly IWallpaperControlClient _wpControlClient;
        private CancellationTokenSource _ctsPreview;
        private readonly SemaphoreSlim _previewSemaphoreSlim = new(1, 1);
    }
}
