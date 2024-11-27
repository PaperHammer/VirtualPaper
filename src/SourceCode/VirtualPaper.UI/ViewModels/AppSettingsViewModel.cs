using VirtualPaper.UIComponent.Utils;
using WinUI3Localizer;

namespace VirtualPaper.UI.ViewModels {
    public class AppSettingsViewModel {
        public string SelBarItem1 { get; set; } = string.Empty;
        public string SelBarItem2 { get; set; } = string.Empty;
        public string SelBarItem3 { get; set; } = string.Empty;
        public string SelBarItem4 { get; set; } = string.Empty;

        public AppSettingsViewModel() {
            _localizer = LanguageUtil.LocalizerInstacne;

            InitText();
        }

        private void InitText() {        
            SelBarItem1 = _localizer.GetLocalizedString("AppSettings_SelBarItem1_General");
            SelBarItem2 = _localizer.GetLocalizedString("AppSettings_SelBarItem2_Performance");
            SelBarItem3 = _localizer.GetLocalizedString("AppSettings_SelBarItem3_System");
            SelBarItem4 = _localizer.GetLocalizedString("AppSettings_SelBarItem4_Others");
        }

        private readonly ILocalizer _localizer;
    }
}
