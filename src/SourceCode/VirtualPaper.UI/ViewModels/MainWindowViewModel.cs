using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Mvvm;
using WinUI3Localizer;

namespace VirtualPaper.UI.ViewModels
{
    internal class MainWindowViewModel : ObservableObject
    {
        public string SidebarGallery { get; set; }
        public string SidebarWpSettings { get; set; }
        public string SidebarAccount { get; set; }
        public string SidebarAppSettings { get; set; }
        public string SidebarHelp { get; set; }

        public MainWindowViewModel()
        {
            _localizer = Localizer.Get();
            _userSettingsClient = App.Services.GetRequiredService<IUserSettingsClient>();

            InitText();
        }

        private void InitText()
        {
            SidebarGallery = _localizer.GetLocalizedString("SidebarGallery");
            SidebarWpSettings = _localizer.GetLocalizedString("SidebarWpSettings");
            SidebarAccount = _localizer.GetLocalizedString("SidebarAccount");
            SidebarAppSettings = _localizer.GetLocalizedString("SidebarAppSettings");
            SidebarHelp = _localizer.GetLocalizedString("SidebarHelp");
        }

        private ILocalizer _localizer;
        private IUserSettingsClient _userSettingsClient;
    }
}
