using WinUI3Localizer;

namespace VirtualPaper.UI.ViewModels
{
    public class AppSettingsViewModel
    {
        public string SelBarItem1 { get; set; } = string.Empty;
        public string SelBarItem2 { get; set; } = string.Empty;
        public string SelBarItem3 { get; set; } = string.Empty;
        public string SelBarItem4 { get; set; } = string.Empty;

        public AppSettingsViewModel()
        {
            InitText();
        }

        private void InitText()
        {
            _localizer = Localizer.Get();

            SelBarItem1 = _localizer.GetLocalizedString("AppSettings_SelBarItem1_General");
            SelBarItem2 = _localizer.GetLocalizedString("AppSettings_SelBarItem2_Performance");
            SelBarItem3 = _localizer.GetLocalizedString("AppSettings_SelBarItem3_System");
            SelBarItem4 = _localizer.GetLocalizedString("AppSettings_SelBarItem4_Others");
        }

        private ILocalizer _localizer;
    }
}
