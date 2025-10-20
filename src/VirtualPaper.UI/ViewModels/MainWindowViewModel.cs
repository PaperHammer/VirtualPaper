using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UI.Utils;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UI.ViewModels {
    public partial class MainWindowViewModel : ObservableObject {
        //public ObservableList<BackgroundTask> BackgroundTasks { get; set; } = [];
        //public string SidebarGallery { get; private set; }
        //public string SidebarWpSettings { get; private set; }
        //public string SidebarProject { get; private set; }
        //public string SidebarAccount { get; private set; }
        //public string SidebarAppSettings { get; private set; }        

        private AppTheme _theme;
        public AppTheme Theme {
            get { return _theme; }
            set { if (_theme == value) return; _theme = value; OnPropertyChanged(); OnThemeChanged(value); }
        }

        private void OnThemeChanged(AppTheme value) {
            ThemeManager.ApplyTheme(value);
            UpdateThemeIcon();
            _userSettings.Settings.ApplicationTheme = value;
            _userSettings.SaveAsync<ISettings>();
        }

        public ICommand? LightAndDarkSwitchCommand { get; private set; }

        private ImageSource _currentThemeIcon;
        public ImageSource CurrentThemeIcon {
            get { return _currentThemeIcon; }
            set { if (_currentThemeIcon == value) return; _currentThemeIcon = value; OnPropertyChanged(); }
        }

        public MainWindowViewModel(IUserSettingsClient userSettingsClient) {
            _userSettings = userSettingsClient;

            _basicComponentUtil = new();
            _dialog = new();

            InitCommand();
        }

        private void InitCommand() {
            LightAndDarkSwitchCommand = new RelayCommand<AppTheme>(item => {
                if (item == Theme) return;

                OnThemeChanged(item);
            });
        }

        private void UpdateThemeIcon() {
            CurrentThemeIcon = Theme switch {
                AppTheme.Auto => (BitmapImage)Application.Current.Resources["NaviIcon_ThemeAuto"],
                AppTheme.Light => (BitmapImage)Application.Current.Resources["NaviIcon_ThemeLight"],
                AppTheme.Dark => (BitmapImage)Application.Current.Resources["NaviIcon_ThemeDark"],
                _ => (BitmapImage)Application.Current.Resources["NaviIcon_ThemeAuto"]
            };
        }

        //private void InitText() {
        //    SidebarGallery = LanguageUtil.GetI18n(Constants.I18n.SidebarGallery);
        //    SidebarWpSettings = LanguageUtil.GetI18n(Constants.I18n.SidebarWpSettings);
        //    SidebarProject = LanguageUtil.GetI18n(Constants.I18n.SidebarProject);
        //    SidebarAccount = LanguageUtil.GetI18n(Constants.I18n.SidebarAccount);
        //    SidebarAppSettings = LanguageUtil.GetI18n(Constants.I18n.SidebarAppSettings);
        //}

        internal readonly BasicComponentUtil _basicComponentUtil;
        internal readonly DialogUtil _dialog;
        internal readonly IUserSettingsClient _userSettings;
    }
}
