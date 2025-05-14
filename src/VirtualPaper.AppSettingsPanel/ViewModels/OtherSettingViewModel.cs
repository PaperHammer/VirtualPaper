using VirtualPaper.Common;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.AppSettingsPanel.ViewModels {
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
            Text_About = LanguageUtil.GetI18n(Constants.I18n.Settings_Others_Text_About);
            About_Basic = LanguageUtil.GetI18n(Constants.I18n.Settings_Others_About_Basic);
            Text_More = LanguageUtil.GetI18n(Constants.I18n.Settings_Others_Text_More);
            More_Document = LanguageUtil.GetI18n(Constants.I18n.Settings_Others_More_Document);
            More_DocumentExplain = LanguageUtil.GetI18n(Constants.I18n.Settings_Others_More_DocumentExplain);
            More_SourceCode = LanguageUtil.GetI18n(Constants.I18n.Settings_Others_More_SourceCode);
            More_SourceCodeExplain = LanguageUtil.GetI18n(Constants.I18n.Settings_Others_More_SourceCodeExplain);
            More_RequestFunc = LanguageUtil.GetI18n(Constants.I18n.Settings_Others_More_RequestFunc);
            More_RequestFuncExplain = LanguageUtil.GetI18n(Constants.I18n.Settings_Others_More_RequestFuncExplain);
            More_ReportBug = LanguageUtil.GetI18n(Constants.I18n.Settings_Others_More_ReportBug);
            More_ReportBugExplain = LanguageUtil.GetI18n(Constants.I18n.Settings_Others_More_ReportBugExplain);
        }
    }
}
