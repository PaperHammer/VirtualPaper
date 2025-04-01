using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.ViewModels {
    public partial class RenameViewModel : ObservableObject {
        private string _newName;
        public string NewName {
            get { return _newName; }
            set {
                _newName = value; 
                IsNameOk = ComplianceUtil.IsValidValueOnlyLength(value);
                OnPropertyChanged();
            }
        }

        private bool _isNameOk;
        public bool IsNameOk {
            get { return _isNameOk; }
            set { _isNameOk = value; OnPropertyChanged(); }
        }

        public string OldName { get; set; }
        public string RenameDialog_Text_AfterChange { get; set; }
        public string RenameDialog_Text_BeforeChange { get; set; }
        public string NewName_InvalidTip { get; set; }

        public RenameViewModel(string oldName) {
            OldName = oldName;

            InitText();
        }

        private void InitText() {
            NewName_InvalidTip = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_NewName_InvalidTip));
            RenameDialog_Text_AfterChange = LanguageUtil.GetI18n(nameof(Constants.I18n.RenameDialog_Text_AfterChange));
            RenameDialog_Text_BeforeChange = LanguageUtil.GetI18n(nameof(Constants.I18n.RenameDialog_Text_BeforeChange));
        }
    }
}
