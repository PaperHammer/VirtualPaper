using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.AccountPanel.ViewModels {
    partial class CloudLibViewModel : ObservableObject {
        public ObservableCollection<IWpBasicData> CloudLibWallpapers { get; set; }
        public string MenuFlyout_Text_DetailAndEditInfo { get; set; } = string.Empty;
        public string MenuFlyout_Text_Downlaod { get; set; } = string.Empty;
        public string MenuFlyout_Text_Preview { get; set; } = string.Empty;
        public string MenuFlyout_Text_Delete { get; set; } = string.Empty;

        public CloudLibViewModel(
            IUserSettingsClient userSettingsClient,
            IAccountClient accountClient,
            IWallpaperControlClient wallpaperControlClient) {
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
            MenuFlyout_Text_Delete = LanguageUtil.GetI18n(nameof(Constants.I18n.Text_DeleteFromDisk));
        }

        internal async Task InitContentAsync() {
            try {
                Account.Instance.GetNotify().Loading(false, false);
                CloudLibWallpapers.Clear();
                _uid2idx.Clear();

                var response = await _accountClient.GetCloudLibAsync();
                if(!response.Success) {
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

        private void UpdateLib(IWpBasicData data) {
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
            throw new NotImplementedException();
        }

        internal async Task PreviewAsync(IWpBasicData data) {
            throw new NotImplementedException();
        }

        internal async Task DownloadAsync(IWpBasicData data) {
            throw new NotImplementedException();
        }

        internal async Task DeleteAsync(IWpBasicData data) {
            throw new NotImplementedException();
        }

        private readonly IAccountClient _accountClient;
        private readonly IWallpaperControlClient _wpControlClient;
        private readonly IUserSettingsClient _userSettingsClient;
        private List<string> _wallpaperInstallFolders;
        private readonly ConcurrentDictionary<string, int> _uid2idx = [];
    }
}
