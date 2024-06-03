using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.Models.WallpaperMetaData;
using WinUI3Localizer;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;
using DispatcherQueueController = Microsoft.UI.Dispatching.DispatcherQueueController;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using NLog;
using VirtualPaper.UI.Views.WpSettingsComponents;

namespace VirtualPaper.UI.ViewModels.WpSettingsComponents
{
    public class WpNavSettingsViewModel : ObservableObject
    {
        public Action InitUpdateLayout;

        private int _wpArrangSelected;
        public int WpArrangSelected
        {
            get { return _wpArrangSelected; }
            set {_wpArrangSelected = value; OnPropertyChanged();}
        }

        public string Text_WpArrange { get; set; } = string.Empty;
        public string WpArrange_Per { get; set; } = string.Empty;
        public string WpArrange_PerExplain { get; set; } = string.Empty;
        public string WpArrange_Expand { get; set; } = string.Empty;
        public string WpArrange_ExpandExplain { get; set; } = string.Empty;
        public string WpArrange_Duplicate { get; set; } = string.Empty;
        public string WpArrange_DuplicateExplain { get; set; } = string.Empty;

        public string Text_VpScreenSaver { get; set; } = string.Empty;
        public string VpScreenSaver_ScreenSaver { get; set; } = string.Empty;
        public string VpScreenSaver_ScreenSaverExplain { get; set; } = string.Empty;
        public string VpScreenSaver_RunningLock { get; set; } = string.Empty;
        public string VpScreenSaver_RunningLockExplain { get; set; } = string.Empty;
        public string VpScreenSaver_WaitingTime { get; set; } = string.Empty;
        public string VpScreenSaver_DynamicEffects { get; set; } = string.Empty;
        public string VpScreenSaver_DynamicEffectsExplain { get; set; } = string.Empty;
        public string VpScreenSaver_WhiteListTitle { get; set; } = string.Empty;
        public string VpScreenSaver_WhiteListExplain { get; set; } = string.Empty;
        public string VpScreenSaver_Add { get; set; } = string.Empty;
        public string VpScreenSaver_SeekFromList { get; set; } = string.Empty;

        private string _screenSaverState = string.Empty;
        public string ScreenSaverStatu
        {
            get => _screenSaverState;
            set { _screenSaverState = value; OnPropertyChanged(); }
        }

        private bool _isScreenSaverOn;
        public bool IsScreenSaverOn
        {
            get => _isScreenSaverOn;
            set
            {
                _isScreenSaverOn = value;
                OnPropertyChanged();
                ChangeScreenSaverStatu(value);
                if (_userSettingsClient.Settings.IsScreenSaverOn == value) return;

                _userSettingsClient.Settings.IsScreenSaverOn = value;
                UpdateSettingsConfigFile();
            }
        }

        private bool _isRunningLock;
        public bool IsRunningLock
        {
            get => _isRunningLock;
            set
            {
                _isRunningLock = value;
                OnPropertyChanged();
                ChangeLockStatu(value);
                if (_userSettingsClient.Settings.IsRunningLock == value) return;

                _userSettingsClient.Settings.IsRunningLock = value;
                UpdateSettingsConfigFile();
            }
        }

        private int _waitingTime = 1;
        public int WaitingTime
        {
            get => _waitingTime;
            set
            {
                _waitingTime = value;
                OnPropertyChanged();
                if (_userSettingsClient.Settings.WaitingTime == value) return;

                _userSettingsClient.Settings.WaitingTime = value;
                UpdateSettingsConfigFile();
            }
        }

        private int _seletedEffectIndx;
        public int SeletedEffectIndx
        {
            get => _seletedEffectIndx;
            set
            {
                _seletedEffectIndx = value;
                OnPropertyChanged();
                if (_userSettingsClient.Settings.ScreenSaverEffect == (ScrEffect)value) return;

                _userSettingsClient.Settings.ScreenSaverEffect = (ScrEffect)value;
                UpdateSettingsConfigFile();
            }
        }

        public List<string> Effects { get; set; } = [];
        public ObservableCollection<ProcInfo> ProcsFiltered { get; set; } = [];

        public WpNavSettingsViewModel(
            IUserSettingsClient userSettingsClient,
            IWallpaperControlClient wallpaperControlClient,
            IScrCommandsClient scrCommandsClient)
        {
            _userSettingsClient = userSettingsClient;
            _wallpaperControlClient = wallpaperControlClient;
            _scrCommandsClient = scrCommandsClient;

            InitText();
            InitCollections();
            InitContent();
        }

        private void InitText()
        {
            _localizer = Localizer.Get();

            Text_WpArrange = _localizer.GetLocalizedString("Settings_WpNav_Text_WpArrange");
            WpArrange_Per = _localizer.GetLocalizedString("Settings_WpNav_WpArrange_Per");
            WpArrange_PerExplain = _localizer.GetLocalizedString("Settings_WpNav_WpArrange_PerExplain");
            WpArrange_Expand = _localizer.GetLocalizedString("Settings_WpNav_WpArrange_Expand");
            WpArrange_ExpandExplain = _localizer.GetLocalizedString("Settings_WpNav_WpArrange_ExpandExplain");
            WpArrange_Duplicate = _localizer.GetLocalizedString("Settings_WpNav_WpArrange_Duplicate");
            WpArrange_DuplicateExplain = _localizer.GetLocalizedString("Settings_WpNav_WpArrange_DuplicateExplain");

            Text_VpScreenSaver = _localizer.GetLocalizedString("Settings_WpNav_Text_VpScreenSaver");
            VpScreenSaver_ScreenSaver = _localizer.GetLocalizedString("Settings_WpNav_VpScreenSaver_ScreenSaver");
            VpScreenSaver_ScreenSaverExplain = _localizer.GetLocalizedString("Settings_WpNav_VpScreenSaver_ScreenSaverExplain");
            VpScreenSaver_RunningLock = _localizer.GetLocalizedString("Settings_WpNav_VpScreenSaver_RunningLock");
            VpScreenSaver_RunningLockExplain = _localizer.GetLocalizedString("Settings_WpNav_VpScreenSaver_RunningLockExplain");
            VpScreenSaver_WaitingTime = _localizer.GetLocalizedString("Settings_WpNav_VpScreenSaver_WaitingTime");
            VpScreenSaver_DynamicEffects = _localizer.GetLocalizedString("Settings_WpNav_VpScreenSaver_DynamicEffects");
            VpScreenSaver_DynamicEffectsExplain = _localizer.GetLocalizedString("Settings_WpNav_VpScreenSaver_DynamicEffectsExplain");
            _effectNone = _localizer.GetLocalizedString("Settings_WpNav_VpScreenSaver__effectNone");
            _effectBubble = _localizer.GetLocalizedString("Settings_WpNav_VpScreenSaver__effectBubble");
            VpScreenSaver_WhiteListTitle = _localizer.GetLocalizedString("Settings_WpNav_VpScreenSaver_WhiteListTitle");
            VpScreenSaver_WhiteListExplain = _localizer.GetLocalizedString("Settings_WpNav_VpScreenSaver_WhiteListExplain");
            VpScreenSaver_Add = _localizer.GetLocalizedString("Settings_WpNav_VpScreenSaver_Add");
            VpScreenSaver_SeekFromList = _localizer.GetLocalizedString("Settings_WpNav_VpScreenSaver_SeekFromList");
        }

        private void InitContent()
        {
            WpArrangSelected = (int)_userSettingsClient.Settings.WallpaperArrangement;
            SeletedEffectIndx = (int)_userSettingsClient.Settings.ScreenSaverEffect;
            IsScreenSaverOn = _userSettingsClient.Settings.IsScreenSaverOn;
            IsRunningLock = _userSettingsClient.Settings.IsRunningLock;
            WaitingTime = _userSettingsClient.Settings.WaitingTime;
        }

        private void InitCollections()
        {
            Effects = [_effectNone, _effectBubble];
            _whiteListScr = [.. _userSettingsClient.Settings.WhiteListScr];
            ProcsFiltered = [.. _userSettingsClient.Settings.WhiteListScr];
        }

        private async void ChangeScreenSaverStatu(bool isScreenSaverOn)
        {
            if (isScreenSaverOn)
            {
                ScreenSaverStatu = _localizer.GetLocalizedString("Settings_WpNav_VpScreenSaver_ScreenSaverStatu_On");

                var wl = _userSettingsClient.WallpaperLayouts;
                if (wl.Count > 0)
                {
                    var wpMetaData = await _wallpaperControlClient.GetWallpaperAsync(wl[0].FolderPath);
                    MetaData metaData = new()
                    {
                        Type = (WallpaperType)wpMetaData.Type,
                        FilePath = wpMetaData.FilePath,
                    };
                    _scrCommandsClient.Start(metaData);
                }
            }
            else
            {
                ScreenSaverStatu = _localizer.GetLocalizedString("Settings_WpNav_VpScreenSaver_ScreenSaverStatu_Off");
                _scrCommandsClient.Stop();
            }
        }

        private void ChangeLockStatu(bool isLock)
        {
            _scrCommandsClient.ChangeLockStatu(isLock);
        }

        private async void UpdateSettingsConfigFile()
        {
            await _userSettingsClient.SaveAsync<ISettings>();
        }

        public async Task UpdateScrSettginsAsync()
        {
            await _userSettingsClient.LoadAsync<ISettings>();
            _ = _dispatcherQueue.TryEnqueue(() =>
            {
                IsScreenSaverOn = _userSettingsClient.Settings.IsScreenSaverOn;
                IsRunningLock = _userSettingsClient.Settings.IsRunningLock;
                SeletedEffectIndx = (int)_userSettingsClient.Settings.ScreenSaverEffect;
            });
        }

        internal async Task UpdateWpArrange(string tag, XamlRoot xamlRoot)
        {
            try
            {
                Loading();

                var type = (WallpaperArrangement)(tag == "Per" ? 0 : tag == "Expand" ? 1 : 2);
                if (type == _userSettingsClient.Settings.WallpaperArrangement) return;

                _userSettingsClient.Settings.WallpaperArrangement = type;
                await _userSettingsClient.SaveAsync<ISettings>();

                InitUpdateLayout?.Invoke();

                var response = await _wallpaperControlClient.RestartAllWallpaperAsync();
                if (response.IsFinished != true)
                {
                    _ = await new ContentDialog()
                    {
                        XamlRoot = xamlRoot,
                        Title = _localizer.GetLocalizedString("Dialog_Title_Error"),
                        Content = response.Msg,
                        PrimaryButtonText = _localizer.GetLocalizedString("Dialog_Btn_Confirm"),
                        DefaultButton = ContentDialogButton.Primary,
                    }.ShowAsync();
                }
            }
            catch { }
            finally
            {
                Loaded();
            }
        }

        internal async void AddToWhiteListScr(ProcInfo procInfo)
        {
            ProcsFiltered.Add(procInfo);

            await Task.Run(() =>
            {
                _whiteListScr.Add(procInfo);
                _scrCommandsClient.AddToWhiteList(procInfo.ProcName);
                _userSettingsClient.Settings.WhiteListScr.Add(procInfo);
                _userSettingsClient.Save<ISettings>();
            });
        }

        internal async void RemoveFromWhiteScr(ProcInfo procInfo)
        {
            ProcsFiltered.Remove(procInfo);

            await Task.Run(() =>
            {
                _whiteListScr.Remove(procInfo);
                _scrCommandsClient.RemoveFromWhiteList(procInfo.ProcName);
                _userSettingsClient.Settings.WhiteListScr.Remove(procInfo);
                if (File.Exists(procInfo.IconPath)) File.Delete(procInfo.IconPath);
                _userSettingsClient.Save<ISettings>();
            });
        }

        private void Loading()
        {
            CheckLoadingEvent();
            _onIsLoading?.Invoke(true);
        }

        private void Loaded()
        {
            CheckLoadingEvent();
            _onIsLoading?.Invoke(false);
        }

        private void CheckLoadingEvent()
        {
            if (_onIsLoading == null)
            {
                CheckViewModel();
                _onIsLoading = _wpSettingsViewModel.IsLoading;
            }
        }

        private void CheckViewModel()
        {
            _wpSettingsViewModel ??= App.Services.GetRequiredService<WpSettingsViewModel>();
        }

        private ILocalizer _localizer;
        private readonly IUserSettingsClient _userSettingsClient;
        private readonly IWallpaperControlClient _wallpaperControlClient;
        private readonly IScrCommandsClient _scrCommandsClient;
        private string _effectNone = string.Empty;
        private string _effectBubble = string.Empty;
        private Action<bool> _onIsLoading;
        private WpSettingsViewModel _wpSettingsViewModel;
        internal ObservableCollection<ProcInfo> _whiteListScr = [];
        private DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? DispatcherQueueController.CreateOnCurrentThread().DispatcherQueue;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    }
}
