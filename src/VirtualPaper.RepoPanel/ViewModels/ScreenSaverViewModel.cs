using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.RepoPanel.ViewModels {
    public partial class ScreenSaverViewModel : ObservableObject, IDisposable {
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
        public ICommand? AddToWhiteListCommand { get; private set; }

        public ScreenSaverViewModel(
            IUserSettingsClient userSettingsClient,
            IScrCommandsClient scrCommandsClient) {
            _userSettingsClient = userSettingsClient;
            _scrCommandsClient = scrCommandsClient;
            _ctsListen = new();

            InitText();
            InitCollections();
            InitContent();
            InitCommand();
        }

        private void InitCommand() {
            AddToWhiteListCommand = new RelayCommand<ProcInfo>(procInfo => {
                PreAddToWhiteList();
            });
        }

        private void InitText() {
            _effectNone = LanguageUtil.GetI18n(Constants.I18n.ScreenSaver__effectNone);
            _effectBubble = LanguageUtil.GetI18n(Constants.I18n.ScreenSaver__effectBubble);
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

        internal async Task ListenForClients() {
            ArcLog.GetLogger<ScreenSaverViewModel>().Info("[PipeServer] Pipe Server is running...");

            try {
                await Task.Run(async () => {
                    while (!_ctsListen.IsCancellationRequested) {
                        using var server = new NamedPipeServerStream("TRAY_CMD", PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
                        await server.WaitForConnectionAsync(_ctsListen.Token);
                        using var reader = new StreamReader(server);
                        string? cmd = await reader.ReadLineAsync(_ctsListen.Token);
                        ArcLog.GetLogger<ScreenSaverViewModel>().Info($"[PipeServer] Received command: {cmd}");

                        if (cmd == "UPDATE_SCRSETTINGS") {
                            await UpdateScrSettginsAsync();
                        }
                    }
                });
            }
            catch (OperationCanceledException) when (_ctsListen.IsCancellationRequested) {
                ArcLog.GetLogger<ScreenSaverViewModel>().Warn("[PipeServer] Listening was canceled.");
            }
            catch (Exception ex) {
                ArcLog.GetLogger<ScreenSaverViewModel>().Error($"[PipeServer] An Error occurred while waiting for or processing client connections: ${ex.Message}");
            }
        }

        internal void StopListenForClients() {
            _ctsListen?.Cancel();
        }

        private void ChangeScreenSaverStatu(bool isScreenSaverOn) {
            if (isScreenSaverOn) {
                ScreenSaverStatu = LanguageUtil.GetI18n(Constants.I18n.Text_On);
                _scrCommandsClient.Start();
            }
            else {
                ScreenSaverStatu = LanguageUtil.GetI18n(Constants.I18n.Text_Off);
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
            try {
                _isLoading = true;
                await _userSettingsClient.LoadAsync<ISettings>();
                CrossThreadInvoker.InvokeOnUIThread(() => {
                    IsScreenSaverOn = _userSettingsClient.Settings.IsScreenSaverOn;
                    IsRunningLock = _userSettingsClient.Settings.IsRunningLock;
                    SeletedEffectIndx = (int)_userSettingsClient.Settings.ScreenSaverEffect;
                });
            }
            finally {
                _isLoading = false;
            }
        }

        private void PreAddToWhiteList() {
            try {
                OpenFileDialog openFileDialog = new() {
                    Filter = "Executable Files (*.exe)|*.exe"
                };
                bool? result = openFileDialog.ShowDialog();

                if (result == true) {
                    string procPath = openFileDialog.FileName;
                    string procName = Path.GetFileNameWithoutExtension(procPath);

                    using System.Drawing.Image img = Win32Util.GetIconByFileName("FILE", procPath).ToBitmap();
                    string iconPath = Path.Combine(Constants.CommonPaths.ExeIconDir, procName) + ".png";
                    img.Save(iconPath);

                    AddToWhiteListScr(new ProcInfo(procName, procPath, iconPath));
                }
            }
            catch (Exception ex) {
                GlobalMessageUtil.ShowException(ex);
                ArcLog.GetLogger<ScreenSaverViewModel>().Error(ex);
            }
        }

        private async void AddToWhiteListScr(ProcInfo procInfo) {
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

        #region Dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    _ctsListen?.Cancel();
                }
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private readonly CancellationTokenSource _ctsListen;
        private readonly IUserSettingsClient _userSettingsClient;
        private readonly IScrCommandsClient _scrCommandsClient;
        private string _effectNone = string.Empty;
        private string _effectBubble = string.Empty;
        internal List<ProcInfo> _whiteListScr = [];
        private bool _isLoading = false;
    }
}
