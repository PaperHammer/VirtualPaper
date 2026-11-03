using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Pipe.Interfaces;
using VirtualPaper.Common.Utils.Storage.Adapter;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.WpSettingsPanel.ViewModels {
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
                ScreenSaverStatu = LanguageUtil.GetI18n(value ? Constants.I18n.Text_On : Constants.I18n.Text_Off);
                if (_userSettingsClient.Settings.IsScreenSaverOn == value) {
                    ChangeScreenSaverStatu(value);
                    return;
                }

                _userSettingsClient.Settings.IsScreenSaverOn = value;
                SaveAndChangeScreenSaverAsync(value);
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
            IScrCommandsClient scrCommandsClient,
            IPipeServerFactory pipeServerFactory,
            IStoragePicker storagePicker) {
            _userSettingsClient = userSettingsClient;
            _scrCommandsClient = scrCommandsClient;
            _pipeServerFactory = pipeServerFactory;
            _storagePicker = storagePicker;

            InitText();
            InitCollections();
            InitContent();
            InitCommand();
        }

        private void InitCommand() {
            AddToWhiteListCommand = new RelayCommand(async () => {
                await PreAddToWhiteListAsync();
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

        internal Task ListenForClients() {
            lock (_listenLock) {
                if (_isDisposed) return Task.CompletedTask;
                if (_ctsListen is { IsCancellationRequested: false }) return _listenTask;
                var previous = _listenTask;
                var source = new CancellationTokenSource();
                _ctsListen = source;
                return _listenTask = ListenForClientsAsync(previous, source);
            }
        }

        private async Task ListenForClientsAsync(Task previous, CancellationTokenSource source) {
            // A re-entered page must wait for the old pipe to release its name.
            await previous.ConfigureAwait(false);
            CancellationToken token = source.Token;
            ArcLog.GetLogger<ScreenSaverViewModel>().Info("[PipeServer] Pipe Server is running...");

            try {
                await Task.Run(async () => {
                    while (!token.IsCancellationRequested) {
                        using var server = _pipeServerFactory.Create(Constants.PipeControlField.TrayCmdPipeName);
                        await server.WaitForConnectionAsync(token);
                        using var reader = new StreamReader(server.GetStream());
                        string? cmd = await reader.ReadLineAsync(token);
                        ArcLog.GetLogger<ScreenSaverViewModel>().Info($"[PipeServer] Received command: {cmd}");

                        if (cmd == Constants.PipeControlField.CmdUpdateScrSettings) {
                            await UpdateScrSettginsAsync();
                        }
                    }
                });
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                ArcLog.GetLogger<ScreenSaverViewModel>().Warn("[PipeServer] Listening was canceled.");
            }
            catch (Exception ex) {
                ArcLog.GetLogger<ScreenSaverViewModel>().Error($"[PipeServer] An Error occurred while waiting for or processing client connections: ${ex.Message}");
            }
            finally {
                lock (_listenLock) {
                    if (ReferenceEquals(_ctsListen, source)) _ctsListen = null;
                    source.Dispose();
                }
            }
        }

        public void StopListenForClients() {
            lock (_listenLock) _ctsListen?.Cancel();
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

        private async void SaveAndChangeScreenSaverAsync(bool enabled) {
            try {
                // Core's StartAsync checks its saved IsScreenSaverOn value.
                // Starting before the settings RPC completes can silently do nothing.
                await _userSettingsClient.SaveAsync<ISettings>();
                if (enabled == _isScreenSaverOn) ChangeScreenSaverStatu(enabled);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<ScreenSaverViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
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

        private async Task PreAddToWhiteListAsync() {
            try {
                var files = await _storagePicker.PickFilesAsync(WindowConsts.WindowHandle, [".exe"], false);

                if (files is { Length: > 0 }) {
                    string procPath = files[0].Path;
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

        internal void AddToWhiteListScr(ProcInfo procInfo) {
            ProcsFiltered.Add(procInfo);

            _whiteListScr.Add(procInfo);
            _scrCommandsClient.AddToWhiteList(procInfo.ProcName);
            _userSettingsClient.Settings.WhiteListScr.Add(procInfo);
            _userSettingsClient.Save<ISettings>();
        }

        internal void RemoveFromWhiteScr(ProcInfo procInfo) {
            ProcsFiltered.Remove(procInfo);

            _whiteListScr.Remove(procInfo);
            _scrCommandsClient.RemoveFromWhiteList(procInfo.ProcName);
            _userSettingsClient.Settings.WhiteListScr.Remove(procInfo);
            if (File.Exists(procInfo.IconPath)) File.Delete(procInfo.IconPath);
            _userSettingsClient.Save<ISettings>();
        }

        #region Dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            lock (_listenLock) {
                if (_isDisposed) return;
                _isDisposed = true;
                if (disposing) {
                    _ctsListen?.Cancel();
                }
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private readonly object _listenLock = new();
        private CancellationTokenSource? _ctsListen;
        private Task _listenTask = Task.CompletedTask;
        private readonly IUserSettingsClient _userSettingsClient;
        private readonly IScrCommandsClient _scrCommandsClient;
        private readonly IPipeServerFactory _pipeServerFactory;
        private readonly IStoragePicker _storagePicker;
        private string _effectNone = string.Empty;
        private string _effectBubble = string.Empty;
        public List<ProcInfo> _whiteListScr = [];
        private bool _isLoading = false;
    }
}
