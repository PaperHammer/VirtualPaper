using VirtualPaper.Common.Utils;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.UIComponent.ViewModels {
    public partial class RenameViewModel : ObservableObject {
        private string? _newName;
        public string? NewName {
            get { return _newName; }
            set {
                _newName = value; 
                IsNameOk = ComplianceUtil.IsValidPathSegmentName(value, 1, _maxLength, _onlyLength);
                OnPropertyChanged();
            }
        }

        private bool _isNameOk;
        public bool IsNameOk {
            get { return _isNameOk; }
            set { _isNameOk = value; OnPropertyChanged(); }
        }

        public string OldName { get; set; } = null!;

        public RenameViewModel(string oldName, int maxLength, bool onlyLength) {
            OldName = oldName;
            _maxLength = maxLength;
            _onlyLength = onlyLength;
        }

        private readonly int _maxLength;
        private readonly bool _onlyLength;
    }
}
