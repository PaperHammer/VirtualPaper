using VirtualPaper.Common.Utils;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.UIComponent.ViewModels {
    public partial class RenameViewModel : ObservableObject {
        private string? _newName;
        public string? NewName {
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

        public string OldName { get; set; } = null!;

        public RenameViewModel(string oldName) {
            OldName = oldName;
        }
    }
}
