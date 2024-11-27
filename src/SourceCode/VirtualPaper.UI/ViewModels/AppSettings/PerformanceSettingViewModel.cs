using System.Collections.Generic;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;
using Windows.ApplicationModel.Resources;
using WinUI3Localizer;

namespace VirtualPaper.UI.ViewModels.AppSettings {
    public partial class PerformanceSettingViewModel : ObservableObject {
        public List<string> PlayStatus { get; set; } = [];
        public List<string> StatuMechanisms { get; set; } = [];
        public string Text_Play { get; set; } = string.Empty;
        public string Play_OthersFullScreen { get; set; } = string.Empty;
        public string Play_OthersFullScreenExplain { get; set; } = string.Empty;
        public string Play_OthersFocus { get; set; } = string.Empty;
        public string Play_OthersFocusExplain { get; set; } = string.Empty;
        public string Play_Audio { get; set; } = string.Empty;
        public string Text_Laptop { get; set; } = string.Empty;
        public string Laptop_BatteryPoweredn { get; set; } = string.Empty;
        public string Laptop_BatteryPowerednExplain { get; set; } = string.Empty;
        public string Laptop_PowerSaving { get; set; } = string.Empty;
        public string Laptop_PowerSavingExplain { get; set; } = string.Empty;
        public string Text_System { get; set; } = string.Empty;
        public string System_RemoteDesktop { get; set; } = string.Empty;
        public string System_RemoteDesktopExplain { get; set; } = string.Empty;
        public string System_StatuMechanism { get; set; } = string.Empty;
        public string System_StatuMechanismExplain_ForPer { get; set; } = string.Empty;
        public string System_StatuMechanismExplain_ForAll { get; set; } = string.Empty;

        private int _selectedFullScreenPlayStatuIndex;
        public int SelectedFullScreenPlayStatuIndex {
            get { return _selectedFullScreenPlayStatuIndex; }
            set {
                _selectedFullScreenPlayStatuIndex = value;
                if (_userSettingsClient.Settings.AppFullscreen == (AppWpRunRulesEnum)value) return;

                _userSettingsClient.Settings.AppFullscreen = (AppWpRunRulesEnum)value;
                UpdateSettingsConfigFile();
                OnPropertyChanged();
            }
        }

        private int _selectedFocusPlayStatuIndex;
        public int SelectedFocusPlayStatuIndex {
            get { return _selectedFocusPlayStatuIndex; }
            set {
                _selectedFocusPlayStatuIndex = value;
                if (_userSettingsClient.Settings.AppFocus == (AppWpRunRulesEnum)value) return;

                _userSettingsClient.Settings.AppFocus = (AppWpRunRulesEnum)value;
                UpdateSettingsConfigFile();
                OnPropertyChanged();
            }
        }

        private string _audioStatu = string.Empty;
        public string AudioStatu {
            get => _audioStatu;
            set { _audioStatu = value; OnPropertyChanged(); }
        }

        private bool _isAudioOnlyOnDesktop;
        public bool IsAudioOnlyOnDesktop {
            get { return _isAudioOnlyOnDesktop; }
            set {
                _isAudioOnlyOnDesktop = value;
                ChangeAudioStatu(value);
                if (_userSettingsClient.Settings.IsAudioOnlyOnDesktop == value) return;

                _userSettingsClient.Settings.IsAudioOnlyOnDesktop = value;
                UpdateSettingsConfigFile();
                OnPropertyChanged();
            }
        }

        private int _selectedBatteryPowerednPlayStatuIndex;
        public int SelectedBatteryPowerednPlayStatuIndex {
            get { return _selectedBatteryPowerednPlayStatuIndex; }
            set {
                _selectedBatteryPowerednPlayStatuIndex = value;
                if (_userSettingsClient.Settings.BatteryPoweredn == (AppWpRunRulesEnum)value) return;

                _userSettingsClient.Settings.BatteryPoweredn = (AppWpRunRulesEnum)value;
                UpdateSettingsConfigFile();
                OnPropertyChanged();
            }
        }

        private int _selectedPowerSavingPlayStatuIndex;
        public int SelectedPowerSavingPlayStatuIndex {
            get { return _selectedPowerSavingPlayStatuIndex; }
            set {
                _selectedPowerSavingPlayStatuIndex = value;
                if (_userSettingsClient.Settings.PowerSaving == (AppWpRunRulesEnum)value) return;

                _userSettingsClient.Settings.PowerSaving = (AppWpRunRulesEnum)value;
                UpdateSettingsConfigFile();
                OnPropertyChanged();
            }
        }

        private int _selectedRemoteDesktopPlayStatuIndex;
        public int SelectedRemoteDesktopPlayStatuIndex {
            get { return _selectedRemoteDesktopPlayStatuIndex; }
            set {
                _selectedRemoteDesktopPlayStatuIndex = value;
                if (_userSettingsClient.Settings.RemoteDesktop == (AppWpRunRulesEnum)value) return;

                _userSettingsClient.Settings.RemoteDesktop = (AppWpRunRulesEnum)value;
                UpdateSettingsConfigFile();
                OnPropertyChanged();
            }
        }

        private int _selectedStatuMechanismPlayStatuIndex;
        public int SelectedStatuMechanismPlayStatuIndex {
            get { return _selectedStatuMechanismPlayStatuIndex; }
            set {
                _selectedStatuMechanismPlayStatuIndex = value;
                if (_userSettingsClient.Settings.StatuMechanism == (StatuMechanismEnum)value) return;

                _userSettingsClient.Settings.StatuMechanism = (StatuMechanismEnum)value;
                UpdateSettingsConfigFile();
                OnPropertyChanged();
            }
        }

        public PerformanceSettingViewModel(
            IUserSettingsClient userSettingsClient) {
            _userSettingsClient = userSettingsClient;
            _localizer = LanguageUtil.LocalizerInstacne;

            InitText();
            InitCollections();
            InitContent();
        }

        private void InitContent() {
            _selectedFullScreenPlayStatuIndex = (int)_userSettingsClient.Settings.AppFullscreen;
            _selectedFocusPlayStatuIndex = (int)_userSettingsClient.Settings.AppFocus;
            IsAudioOnlyOnDesktop = _userSettingsClient.Settings.IsAudioOnlyOnDesktop;
            _selectedBatteryPowerednPlayStatuIndex = (int)_userSettingsClient.Settings.BatteryPoweredn;
            _selectedPowerSavingPlayStatuIndex = (int)_userSettingsClient.Settings.PowerSaving;
            _selectedRemoteDesktopPlayStatuIndex = (int)_userSettingsClient.Settings.RemoteDesktop;
            _selectedStatuMechanismPlayStatuIndex = (int)_userSettingsClient.Settings.StatuMechanism;
        }

        private void InitCollections() {
            PlayStatus = [_playStatu_Silence, _playStatu_Pause, _playStatu_KeepRun];
            StatuMechanisms = [_statuMechanism_per, _statuMechanism_all];
        }

        private void InitText() {
            Text_Play = _localizer.GetLocalizedString("Settings_Perforemance_Text_Play");
            Play_OthersFullScreen = _localizer.GetLocalizedString("Settings_Perforemance_Play_OthersFullScreen");
            Play_OthersFullScreenExplain = _localizer.GetLocalizedString("Settings_Perforemance_Play_OthersFullScreenExplain");
            _playStatu_Silence = _localizer.GetLocalizedString("Settings_Perforemance_Play__playStatu_Silence");
            _playStatu_Pause = _localizer.GetLocalizedString("Settings_Perforemance_Play__playStatu_Pause");
            _playStatu_KeepRun = _localizer.GetLocalizedString("Settings_Perforemance_Play__playStatu_KeepRun");
            Play_OthersFocus = _localizer.GetLocalizedString("Settings_Perforemance_Play_OthersFocus");
            Play_OthersFocusExplain = _localizer.GetLocalizedString("Settings_Perforemance_Play_OthersFocusExplain");
            Play_Audio = _localizer.GetLocalizedString("Settings_Perforemance_Play_Audio");
            Text_Laptop = _localizer.GetLocalizedString("Settings_Perforemance_Text_Laptop");
            Laptop_BatteryPoweredn = _localizer.GetLocalizedString("Settings_Perforemance_Laptop_BatteryPoweredn");
            Laptop_BatteryPowerednExplain = _localizer.GetLocalizedString("Settings_Perforemance_Laptop_BatteryPowerednExplain");
            Laptop_PowerSaving = _localizer.GetLocalizedString("Settings_Perforemance_Laptop_PowerSaving");
            Laptop_PowerSavingExplain = _localizer.GetLocalizedString("Settings_Perforemance_Laptop_PowerSavingExplain");
            Text_System = _localizer.GetLocalizedString("Settings_Perforemance_Text_System");
            System_RemoteDesktop = _localizer.GetLocalizedString("Settings_Perforemance_System_RemoteDesktop");
            System_RemoteDesktopExplain = _localizer.GetLocalizedString("Settings_Perforemance_System_RemoteDesktopExplain");
            System_StatuMechanism = _localizer.GetLocalizedString("Settings_Perforemance_System_StatuMechanism");
            System_StatuMechanismExplain_ForPer = _localizer.GetLocalizedString("Settings_Perforemance_System_StatuMechanismExplain_ForPer");
            System_StatuMechanismExplain_ForAll = _localizer.GetLocalizedString("Settings_Perforemance_System_StatuMechanismExplain_ForAll");
            _statuMechanism_per = _localizer.GetLocalizedString("Settings_Perforemance_System__statuMechanism_per");
            _statuMechanism_all = _localizer.GetLocalizedString("Settings_Perforemance_System__statuMechanism_all");
        }

        private void ChangeAudioStatu(bool isAudioOnlyOnDesktop) {
            if (isAudioOnlyOnDesktop) {
                AudioStatu = _localizer.GetLocalizedString("Settings_Perforemance_Play_Audio_OnlyDesktop_On");
            }
            else {
                AudioStatu = _localizer.GetLocalizedString("Settings_Perforemance_Play_Audio_OnlyDesktop_Off");
            }
        }

        private async void UpdateSettingsConfigFile() {
            await _userSettingsClient.SaveAsync<ISettings>();
        }

        private readonly ILocalizer _localizer;
        private string _playStatu_Silence = string.Empty;
        private string _playStatu_Pause = string.Empty;
        private string _playStatu_KeepRun = string.Empty;
        private string _statuMechanism_per = string.Empty;
        private string _statuMechanism_all = string.Empty;
        private IUserSettingsClient _userSettingsClient;
    }
}
