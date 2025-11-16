using System.Collections.Generic;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.AppSettingsPanel.ViewModels {
    public partial class PerformanceSettingViewModel : ObservableObject {
        public List<string> PlayStatus { get; set; } = [];
        public List<string> StatuMechanisms { get; set; } = [];        

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
            _playStatu_Silence = LanguageUtil.GetI18n(Constants.I18n.Settings_Perforemance_Play__playStatu_Silence);
            _playStatu_Pause = LanguageUtil.GetI18n(Constants.I18n.Settings_Perforemance_Play__playStatu_Pause);
            _playStatu_KeepRun = LanguageUtil.GetI18n(Constants.I18n.Settings_Perforemance_Play__playStatu_KeepRun);
            _statuMechanism_per = LanguageUtil.GetI18n(Constants.I18n.Settings_Perforemance_System__statuMechanism_per);
            _statuMechanism_all = LanguageUtil.GetI18n(Constants.I18n.Settings_Perforemance_System__statuMechanism_all);
        }

        private void ChangeAudioStatu(bool isAudioOnlyOnDesktop) {
            AudioStatu = LanguageUtil.GetI18n(isAudioOnlyOnDesktop ? Constants.I18n.Text_On : Constants.I18n.Text_Off);
        }

        private async void UpdateSettingsConfigFile() {
            await _userSettingsClient.SaveAsync<ISettings>();
        }

        private string _playStatu_Silence = string.Empty;
        private string _playStatu_Pause = string.Empty;
        private string _playStatu_KeepRun = string.Empty;
        private string _statuMechanism_per = string.Empty;
        private string _statuMechanism_all = string.Empty;
        private readonly IUserSettingsClient _userSettingsClient;
    }
}
