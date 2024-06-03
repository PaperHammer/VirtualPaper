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

        public MainWindowViewModel()
        {
            _localizer = Localizer.Get();

            InitText();
        }

        private void InitText()
        {
            SidebarGallery = _localizer.GetLocalizedString("SidebarGallery");
            SidebarWpSettings = _localizer.GetLocalizedString("SidebarWpSettings");
            SidebarAccount = _localizer.GetLocalizedString("SidebarAccount");
            SidebarAppSettings = _localizer.GetLocalizedString("SidebarAppSettings");
        }

        private ILocalizer _localizer;
    }
}
