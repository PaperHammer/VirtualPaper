using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.UI.ViewModels.WpSettingsComponents {
    public partial class ScreenSaverViewModel : ObservableObject {
        public string ScreenSaver_Server { get; set; } = string.Empty;
        public string ScreenSaver_ServerExplain { get; set; } = string.Empty;
        public string ScreenSaver_RunningLock { get; set; } = string.Empty;
        public string ScreenSaver_RunningLockExplain { get; set; } = string.Empty;
        public string ScreenSaver_WaitingTime { get; set; } = string.Empty;
        public string ScreenSaver_DynamicEffects { get; set; } = string.Empty;
        public string ScreenSaver_DynamicEffectsExplain { get; set; } = string.Empty;
        public string ScreenSaver_WhiteListTitle { get; set; } = string.Empty;
        public string ScreenSaver_WhiteListExplain { get; set; } = string.Empty;
        public string ScreenSaver_Add { get; set; } = string.Empty;
        public string ScreenSaver_SeekFromList { get; set; } = string.Empty;
        public string Text_Delete { get; set; } = string.Empty;

        private string _screenSaverState = string.Empty;
        public string ScreenSaverStatu {
            get => _screenSaverState;
            set { _screenSaverState = value; OnPropertyChanged(); }
        }

        private bool _isScreenSaverOn;
        public bool IsScreenSaverOn {
            get => _isScreenSaverOn;
            set {
                _isScreenSaverOn = value;
                OnPropertyChanged();
                ChangeScreenSaverStatu(value);
                if (_userSettingsClient.Settings.IsScreenSaverOn == value) return;

                _userSettingsClient.Settings.IsScreenSaverOn = value;
                UpdateSettingsConfigFile();
            }
        }

        private bool _isRunningLock;
        public bool IsRunningLock {
            get => _isRunningLock;
            set {
                _isRunningLock = value;
                OnPropertyChanged();
                ChangeLockStatu(value);
                if (_userSettingsClient.Settings.IsRunningLock == value) return;

                _userSettingsClient.Settings.IsRunningLock = value;
                UpdateSettingsConfigFile();
            }
        }

        private int _waitingTime = 1;
        public int WaitingTime {
            get => _waitingTime;
            set {
                _waitingTime = value;
                OnPropertyChanged();
                if (_userSettingsClient.Settings.WaitingTime == value) return;

                _userSettingsClient.Settings.WaitingTime = value;
                UpdateSettingsConfigFile();
            }
        }

        private int _seletedEffectIndx;
        public int SeletedEffectIndx {
            get => _seletedEffectIndx;
            set {
                _seletedEffectIndx = value;
                OnPropertyChanged();
                if (_userSettingsClient.Settings.ScreenSaverEffect == (ScrEffect)value) return;

                _userSettingsClient.Settings.ScreenSaverEffect = (ScrEffect)value;
                UpdateSettingsConfigFile();
            }
        }

        public List<string> Effects { get; set; } = [];
        public ObservableCollection<ProcInfo> ProcsFiltered { get; set; } = [];

        public ScreenSaverViewModel(
            IUserSettingsClient userSettingsClient,
            IScrCommandsClient scrCommandsClient) {
            _userSettingsClient = userSettingsClient;
            _scrCommandsClient = scrCommandsClient;

            InitText();
            InitCollections();
            InitContent();
        }

        private void InitText() {
            ScreenSaver_Server = App.GetI18n(Constants.I18n.ScreenSaver_Server);
            ScreenSaver_ServerExplain = App.GetI18n(Constants.I18n.ScreenSaver_ServerExplain);
            ScreenSaver_RunningLock = App.GetI18n(Constants.I18n.ScreenSaver_RunningLock);
            ScreenSaver_RunningLockExplain = App.GetI18n(Constants.I18n.ScreenSaver_RunningLockExplain);
            ScreenSaver_WaitingTime = App.GetI18n(Constants.I18n.ScreenSaver_WaitingTime);
            ScreenSaver_DynamicEffects = App.GetI18n(Constants.I18n.ScreenSaver_DynamicEffects);
            ScreenSaver_DynamicEffectsExplain = App.GetI18n(Constants.I18n.ScreenSaver_DynamicEffectsExplain);
            _effectNone = App.GetI18n(Constants.I18n.ScreenSaver__effectNone);
            _effectBubble = App.GetI18n(Constants.I18n.ScreenSaver__effectBubble);
            ScreenSaver_WhiteListTitle = App.GetI18n(Constants.I18n.ScreenSaver_WhiteListTitle);
            ScreenSaver_WhiteListExplain = App.GetI18n(Constants.I18n.ScreenSaver_WhiteListExplain);
            ScreenSaver_Add = App.GetI18n(Constants.I18n.ScreenSaver_Add);
            ScreenSaver_SeekFromList = App.GetI18n(Constants.I18n.ScreenSaver_SeekFromList);
            Text_Delete = App.GetI18n(Constants.I18n.Text_Delete);
        }

        private void InitContent() {
            SeletedEffectIndx = (int)_userSettingsClient.Settings.ScreenSaverEffect;
            IsScreenSaverOn = _userSettingsClient.Settings.IsScreenSaverOn;
            IsRunningLock = _userSettingsClient.Settings.IsRunningLock;
            WaitingTime = _userSettingsClient.Settings.WaitingTime;
        }

        private void InitCollections() {
            Effects = [_effectNone, _effectBubble];
            _whiteListScr = [.. _userSettingsClient.Settings.WhiteListScr];
            ProcsFiltered = [.. _userSettingsClient.Settings.WhiteListScr];
        }

        private void ChangeScreenSaverStatu(bool isScreenSaverOn) {
            if (isScreenSaverOn) {
                ScreenSaverStatu = App.GetI18n(Constants.I18n.ScreenSaver_ServerStatu_On);
                _scrCommandsClient.Start();
            }
            else {
                ScreenSaverStatu = App.GetI18n(Constants.I18n.ScreenSaver_ServerStatu_Off);
                _scrCommandsClient.Stop();
            }
        }

        private void ChangeLockStatu(bool isLock) {
            _scrCommandsClient.ChangeLockStatu(isLock);
        }

        private async void UpdateSettingsConfigFile() {
            await _userSettingsClient.SaveAsync<ISettings>();
        }

        public async Task UpdateScrSettginsAsync() {
            await _userSettingsClient.LoadAsync<ISettings>();
            App.UITaskInvokeQueue.TryEnqueue(() => {
                IsScreenSaverOn = _userSettingsClient.Settings.IsScreenSaverOn;
                IsRunningLock = _userSettingsClient.Settings.IsRunningLock;
                SeletedEffectIndx = (int)_userSettingsClient.Settings.ScreenSaverEffect;
            });
        }

        internal async void AddToWhiteListScr(ProcInfo procInfo) {
            ProcsFiltered.Add(procInfo);

            await Task.Run(() => {
                _whiteListScr.Add(procInfo);
                _scrCommandsClient.AddToWhiteList(procInfo.ProcName);
                _userSettingsClient.Settings.WhiteListScr.Add(procInfo);
                _userSettingsClient.Save<ISettings>();
            });
        }

        internal async void RemoveFromWhiteScr(ProcInfo procInfo) {
            ProcsFiltered.Remove(procInfo);

            await Task.Run(() => {
                _whiteListScr.Remove(procInfo);
                _scrCommandsClient.RemoveFromWhiteList(procInfo.ProcName);
                _userSettingsClient.Settings.WhiteListScr.Remove(procInfo);
                if (File.Exists(procInfo.IconPath)) File.Delete(procInfo.IconPath);
                _userSettingsClient.Save<ISettings>();
            });
        }

        private readonly IUserSettingsClient _userSettingsClient;
        private readonly IScrCommandsClient _scrCommandsClient;
        private string _effectNone = string.Empty;
        private string _effectBubble = string.Empty;
        internal ObservableCollection<ProcInfo> _whiteListScr = [];
    }
}
