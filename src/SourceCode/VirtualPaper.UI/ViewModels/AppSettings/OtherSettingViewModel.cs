using VirtualPaper.Common;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.UI.ViewModels.AppSettings {
    public partial class OtherSettingViewModel : ObservableObject {
        public string Text_About { get; set; } = string.Empty;
        public string About_Basic { get; set; } = string.Empty;
        public string Text_More { get; set; } = string.Empty;
        public string More_Document { get; set; } = string.Empty;
        public string More_DocumentExplain { get; set; } = string.Empty;
        public string More_SourceCode { get; set; } = string.Empty;
        public string More_SourceCodeExplain { get; set; } = string.Empty;
        public string More_RequestFunc { get; set; } = string.Empty;
        public string More_RequestFuncExplain { get; set; } = string.Empty;
        public string More_ReportBug { get; set; } = string.Empty;
        public string More_ReportBugExplain { get; set; } = string.Empty;

        public string More_RequestFunc_Link { get; } = "https://github.com/PaperHammer/VirtualPaper/issues/new?assignees=&labels=Needs-Triage&projects=&template=feature_request.yml";

        public OtherSettingViewModel() {
            InitText();
        }

        private void InitText() {
            Text_About = App.GetI18n(Constants.I18n.Settings_Others_Text_About);
            About_Basic = App.GetI18n(Constants.I18n.Settings_Others_About_Basic);
            Text_More = App.GetI18n(Constants.I18n.Settings_Others_Text_More);
            More_Document = App.GetI18n(Constants.I18n.Settings_Others_More_Document);
            More_DocumentExplain = App.GetI18n(Constants.I18n.Settings_Others_More_DocumentExplain);
            More_SourceCode = App.GetI18n(Constants.I18n.Settings_Others_More_SourceCode);
            More_SourceCodeExplain = App.GetI18n(Constants.I18n.Settings_Others_More_SourceCodeExplain);
            More_RequestFunc = App.GetI18n(Constants.I18n.Settings_Others_More_RequestFunc);
            More_RequestFuncExplain = App.GetI18n(Constants.I18n.Settings_Others_More_RequestFuncExplain);
            More_ReportBug = App.GetI18n(Constants.I18n.Settings_Others_More_ReportBug);
            More_ReportBugExplain = App.GetI18n(Constants.I18n.Settings_Others_More_ReportBugExplain);
        }
    }
}
