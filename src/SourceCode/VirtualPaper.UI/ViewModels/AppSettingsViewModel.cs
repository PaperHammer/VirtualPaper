using VirtualPaper.Common;

namespace VirtualPaper.UI.ViewModels {
    public class AppSettingsViewModel {
        public string SelBarItem1 { get; set; } = string.Empty;
        public string SelBarItem2 { get; set; } = string.Empty;
        public string SelBarItem3 { get; set; } = string.Empty;
        public string SelBarItem4 { get; set; } = string.Empty;

        public AppSettingsViewModel() {
            InitText();
        }

        private void InitText() {        
            SelBarItem1 = App.GetI18n(Constants.I18n.AppSettings_SelBarItem1_General);
            SelBarItem2 = App.GetI18n(Constants.I18n.AppSettings_SelBarItem2_Performance);
            SelBarItem3 = App.GetI18n(Constants.I18n.AppSettings_SelBarItem3_System);
            SelBarItem4 = App.GetI18n(Constants.I18n.AppSettings_SelBarItem4_Others);
        }
    }
}
