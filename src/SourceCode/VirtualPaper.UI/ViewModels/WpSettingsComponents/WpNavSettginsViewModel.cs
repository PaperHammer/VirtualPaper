using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using WinUI3Localizer;

namespace VirtualPaper.UI.ViewModels.WpSettingsComponents
{
    public class WpNavSettginsViewModel : ObservableObject
    {
        private int _wpArrangSelected;
        public int WpArrangSelected
        {
            get { return _wpArrangSelected; }
            set { _wpArrangSelected = value; OnPropertyChanged(); }
        }

        public string Text_WpArrange { get; set; } = string.Empty;
        public string WpArrange_Per { get; set; } = string.Empty;
        public string WpArrange_PerExplain { get; set; } = string.Empty;
        public string WpArrange_Expand { get; set; } = string.Empty;
        public string WpArrange_ExpandExplain { get; set; } = string.Empty;
        public string WpArrange_Duplicate { get; set; } = string.Empty;
        public string WpArrange_DuplicateExplain { get; set; } = string.Empty;

        public WpNavSettginsViewModel(
            IUserSettingsClient userSettingsClient, 
            IWallpaperControlClient wallpaperControlClient)
        {
            _userSettingsClient = userSettingsClient;
            _wallpaperControlClient = wallpaperControlClient;
            _wpSettingsViewModel = App.Services.GetRequiredService<WpSettingsViewModel>();

            InitText();
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
        }

        private void InitContent()
        {
            var wpArrange = _userSettingsClient.Settings.WallpaperArrangement;
            WpArrangSelected = (int)wpArrange;
        }

        internal async Task UpdateWpArrange(string tag)
        {
            var type = (WallpaperArrangement)(tag == "Per" ? 0 : tag == "Expand" ? 1 : 2);
            if (type == _userSettingsClient.Settings.WallpaperArrangement) return;

            _userSettingsClient.Settings.WallpaperArrangement = type;                

            await _userSettingsClient.SaveAsync<ISettings>();
            _wpSettingsViewModel.InitUpdateLayout();
            await _wallpaperControlClient.RestartAllWallpaperAsync();
        }

        private ILocalizer _localizer;
        private IUserSettingsClient _userSettingsClient;
        private IWallpaperControlClient _wallpaperControlClient;
        private WpSettingsViewModel _wpSettingsViewModel;
    }
}
